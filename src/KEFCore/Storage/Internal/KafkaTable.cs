/*
*  Copyright 2022 MASES s.r.l.
*
*  Licensed under the Apache License, Version 2.0 (the "License");
*  you may not use this file except in compliance with the License.
*  You may obtain a copy of the License at
*
*  http://www.apache.org/licenses/LICENSE-2.0
*
*  Unless required by applicable law or agreed to in writing, software
*  distributed under the License is distributed on an "AS IS" BASIS,
*  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
*  See the License for the specific language governing permissions and
*  limitations under the License.
*
*  Refer to LICENSE for more information.
*/

#nullable enable

using System.Collections;
using System.Globalization;
using MASES.KNet.Streams.KStream;
using MASES.EntityFrameworkCore.KNet.Internal;
using MASES.EntityFrameworkCore.KNet.ValueGeneration.Internal;
using MASES.KNet.Clients.Producer;
using Java.Util.Concurrent;
using MASES.EntityFrameworkCore.KNet.Serdes.Internal;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

public class KafkaTable<TKey> : IKafkaTable
    where TKey : notnull
{
    private readonly IPrincipalKeyValueFactory<TKey> _keyValueFactory;
    private readonly bool _sensitiveLoggingEnabled;
    private readonly Dictionary<TKey, object?[]> _rows;
    private readonly IList<(int, ValueConverter)>? _valueConverters;
    private readonly IList<(int, ValueComparer)>? _valueComparers;

    private Dictionary<int, IKafkaIntegerValueGenerator>? _integerGenerators;

    private readonly IProducer<string, string> _kafkaProducer;
    private readonly string _tableAssociatedTopicName;
    private readonly IKafkaSerdesEntityType _serdes;

    public KafkaTable(
        IKafkaCluster cluster,
        IEntityType entityType,
        bool sensitiveLoggingEnabled)
    {
        Cluster = cluster;
        EntityType = entityType;

        cluster.CreateTable(entityType, out _tableAssociatedTopicName);
        _serdes = cluster.CreateSerdes(entityType);
        _kafkaProducer = cluster.CreateProducer(entityType);

        _keyValueFactory = entityType.FindPrimaryKey()!.GetPrincipalKeyValueFactory<TKey>();
        _sensitiveLoggingEnabled = sensitiveLoggingEnabled;
        _rows = new Dictionary<TKey, object?[]>(_keyValueFactory.EqualityComparer);

        foreach (var property in entityType.GetProperties())
        {
            var converter = property.GetValueConverter()
                ?? property.FindTypeMapping()?.Converter;

            if (converter != null)
            {
                _valueConverters ??= new System.Collections.Generic.List<(int, ValueConverter)>();
                _valueConverters.Add((property.GetIndex(), converter));
            }

            var comparer = property.GetKeyValueComparer();
            if (!comparer.IsDefault())
            {
                _valueComparers ??= new System.Collections.Generic.List<(int, ValueComparer)>();
                _valueComparers.Add((property.GetIndex(), comparer));
            }
        }
    }

    public virtual IKafkaCluster Cluster { get; }

    public virtual IEntityType EntityType { get; }

    public virtual KafkaIntegerValueGenerator<TProperty> GetIntegerValueGenerator<TProperty>(
        IProperty property,
        IReadOnlyList<IKafkaTable> tables)
    {
        _integerGenerators ??= new Dictionary<int, IKafkaIntegerValueGenerator>();

        var propertyIndex = property.GetIndex();
        if (!_integerGenerators.TryGetValue(propertyIndex, out var generator))
        {
            generator = new KafkaIntegerValueGenerator<TProperty>(propertyIndex);
            _integerGenerators[propertyIndex] = generator;

            foreach (var table in tables)
            {
                foreach (var row in table.Rows)
                {
                    generator.Bump(row);
                }
            }
        }

        return (KafkaIntegerValueGenerator<TProperty>)generator;
    }

    public virtual IEnumerable<object?[]> Rows
        => RowsInTable();

    public virtual IReadOnlyList<object?[]> SnapshotRows()
    {
        var rows = Rows.ToList();
        var rowCount = rows.Count;
        var properties = EntityType.GetProperties().ToList();
        var propertyCount = properties.Count;

        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var snapshotRow = new object?[propertyCount];
            Array.Copy(rows[rowIndex], snapshotRow, propertyCount);

            if (_valueConverters != null)
            {
                foreach (var (index, converter) in _valueConverters)
                {
                    snapshotRow[index] = converter.ConvertFromProvider(snapshotRow[index]);
                }
            }

            if (_valueComparers != null)
            {
                foreach (var (index, comparer) in _valueComparers)
                {
                    snapshotRow[index] = comparer.Snapshot(snapshotRow[index]);
                }
            }

            rows[rowIndex] = snapshotRow;
        }

        return rows;
    }

    private IEnumerable<object?[]> RowsInTable()
    {
        return _rows.Values;
    }

    private static System.Collections.Generic.List<ValueComparer> GetKeyComparers(IEnumerable<IProperty> properties)
        => properties.Select(p => p.GetKeyValueComparer()).ToList();

    public virtual ProducerRecord<string, string> Create(IUpdateEntry entry)
    {
        var properties = entry.EntityType.GetProperties().ToList();
        var row = new object?[properties.Count];
        var nullabilityErrors = new System.Collections.Generic.List<IProperty>();
        var key = CreateKey(entry);

        for (var index = 0; index < properties.Count; index++)
        {
            var propertyValue = SnapshotValue(properties[index], properties[index].GetKeyValueComparer(), entry);

            row[index] = propertyValue;
            HasNullabilityError(properties[index], propertyValue, nullabilityErrors);
        }

        if (nullabilityErrors.Count > 0)
        {
            ThrowNullabilityErrorException(entry, nullabilityErrors);
        }

        _rows.Add(key, row);

        BumpValueGenerators(row);

        return NewRecord(entry, key, row);
    }

    public virtual ProducerRecord<string, string> Delete(IUpdateEntry entry)
    {
        var key = CreateKey(entry);

        if (_rows.TryGetValue(key, out var row))
        {
            var properties = entry.EntityType.GetProperties().ToList();
            var concurrencyConflicts = new Dictionary<IProperty, object?>();

            for (var index = 0; index < properties.Count; index++)
            {
                IsConcurrencyConflict(entry, properties[index], row[index], concurrencyConflicts);
            }

            if (concurrencyConflicts.Count > 0)
            {
                ThrowUpdateConcurrencyException(entry, concurrencyConflicts);
            }

            _rows.Remove(key);

            return NewRecord(entry, key, null);
        }
        else
        {
            throw new DbUpdateConcurrencyException(KafkaStrings.UpdateConcurrencyException, new[] { entry });
        }
    }

    private static bool IsConcurrencyConflict(
        IUpdateEntry entry,
        IProperty property,
        object? rowValue,
        Dictionary<IProperty, object?> concurrencyConflicts)
    {
        if (property.IsConcurrencyToken)
        {
            var comparer = property.GetKeyValueComparer();
            var originalValue = entry.GetOriginalValue(property);

            var converter = property.GetValueConverter()
                ?? property.FindTypeMapping()?.Converter;

            if (converter != null)
            {
                rowValue = converter.ConvertFromProvider(rowValue);
            }

            if ((comparer != null && !comparer.Equals(rowValue, originalValue))
                || (comparer == null && !StructuralComparisons.StructuralEqualityComparer.Equals(rowValue, originalValue)))
            {
                concurrencyConflicts.Add(property, rowValue);

                return true;
            }
        }

        return false;
    }

    public virtual ProducerRecord<string, string> Update(IUpdateEntry entry)
    {
        var key = CreateKey(entry);

        if (_rows.TryGetValue(key, out var row))
        {
            var properties = entry.EntityType.GetProperties().ToList();
            var comparers = GetKeyComparers(properties);
            var valueBuffer = new object?[properties.Count];
            var concurrencyConflicts = new Dictionary<IProperty, object?>();
            var nullabilityErrors = new System.Collections.Generic.List<IProperty>();

            for (var index = 0; index < valueBuffer.Length; index++)
            {
                if (IsConcurrencyConflict(entry, properties[index], row[index], concurrencyConflicts))
                {
                    continue;
                }

                if (HasNullabilityError(properties[index], row[index], nullabilityErrors))
                {
                    continue;
                }

                valueBuffer[index] = entry.IsModified(properties[index])
                    ? SnapshotValue(properties[index], comparers[index], entry)
                    : row[index];
            }

            if (concurrencyConflicts.Count > 0)
            {
                ThrowUpdateConcurrencyException(entry, concurrencyConflicts);
            }

            if (nullabilityErrors.Count > 0)
            {
                ThrowNullabilityErrorException(entry, nullabilityErrors);
            }

            _rows[key] = valueBuffer;

            BumpValueGenerators(valueBuffer);

            return NewRecord(entry, key, valueBuffer);
        }
        else
        {
            throw new DbUpdateConcurrencyException(KafkaStrings.UpdateConcurrencyException, new[] { entry });
        }
    }

    public virtual IEnumerable<Future<RecordMetadata>> Commit(IEnumerable<ProducerRecord<string, string>> records)
    {
        System.Collections.Generic.List<Future<RecordMetadata>> futures = new();
        foreach (var record in records)
        {
            var future = _kafkaProducer.Send(record);
            futures.Add(future);
        }

        _kafkaProducer.Flush();

        return futures;
    }

    public virtual void BumpValueGenerators(object?[] row)
    {
        if (_integerGenerators != null)
        {
            foreach (var generator in _integerGenerators.Values)
            {
                generator.Bump(row);
            }
        }
    }

    private ProducerRecord<string, string> NewRecord(IUpdateEntry entry, TKey key, object?[]? row)
    {
        var record = new ProducerRecord<string, string>(_tableAssociatedTopicName, _serdes.Serialize<TKey>(key), _serdes.Serialize(row));

        return record;
    }

    private TKey CreateKey(IUpdateEntry entry)
        => _keyValueFactory.CreateFromCurrentValues(entry);

    private static object? SnapshotValue(IProperty property, ValueComparer? comparer, IUpdateEntry entry)
    {
        var value = SnapshotValue(comparer, entry.GetCurrentValue(property));

        var converter = property.GetValueConverter()
            ?? property.FindTypeMapping()?.Converter;

        if (converter != null)
        {
            value = converter.ConvertToProvider(value);
        }

        return value;
    }

    private static object? SnapshotValue(ValueComparer? comparer, object? value)
        => comparer == null ? value : comparer.Snapshot(value);

    private bool HasNullabilityError(
        IProperty property,
        object? propertyValue,
        IList<IProperty> nullabilityErrors)
    {
        if (!property.IsNullable && propertyValue == null)
        {
            nullabilityErrors.Add(property);

            return true;
        }

        return false;
    }

    private void ThrowNullabilityErrorException(
        IUpdateEntry entry,
        IList<IProperty> nullabilityErrors)
    {
        if (_sensitiveLoggingEnabled)
        {
            throw new DbUpdateException(
                KafkaStrings.NullabilityErrorExceptionSensitive(
                    nullabilityErrors.Format(),
                    entry.EntityType.DisplayName(),
                    entry.BuildCurrentValuesString(entry.EntityType.FindPrimaryKey()!.Properties)),
                new[] { entry });
        }

        throw new DbUpdateException(
            KafkaStrings.NullabilityErrorException(
                nullabilityErrors.Format(),
                entry.EntityType.DisplayName()),
            new[] { entry });
    }

    /// <summary>
    ///     Throws an exception indicating that concurrency conflicts were detected.
    /// </summary>
    /// <param name="entry">The update entry which resulted in the conflict(s).</param>
    /// <param name="concurrencyConflicts">The conflicting properties with their associated database values.</param>
    protected virtual void ThrowUpdateConcurrencyException(
        IUpdateEntry entry,
        Dictionary<IProperty, object?> concurrencyConflicts)
    {
        if (_sensitiveLoggingEnabled)
        {
            throw new DbUpdateConcurrencyException(
                KafkaStrings.UpdateConcurrencyTokenExceptionSensitive(
                    entry.EntityType.DisplayName(),
                    entry.BuildCurrentValuesString(entry.EntityType.FindPrimaryKey()!.Properties),
                    entry.BuildOriginalValuesString(concurrencyConflicts.Keys),
                    "{"
                    + string.Join(
                        ", ",
                        concurrencyConflicts.Select(
                            c => c.Key.Name + ": " + Convert.ToString(c.Value, CultureInfo.InvariantCulture)))
                    + "}"),
                new[] { entry });
        }

        throw new DbUpdateConcurrencyException(
            KafkaStrings.UpdateConcurrencyTokenException(
                entry.EntityType.DisplayName(),
                concurrencyConflicts.Keys.Format()),
            new[] { entry });
    }
}
