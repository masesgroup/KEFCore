/*
*  Copyright (c) 2022-2026 MASES s.r.l.
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

//#define DEBUG_PERFORMANCE

#nullable enable

using Java.Util.Concurrent;
using MASES.EntityFrameworkCore.KNet.Internal;
using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.EntityFrameworkCore.KNet.ValueGeneration.Internal;
using Org.Apache.Kafka.Clients.Producer;
using System.Collections;
using System.Globalization;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaTable<TKey, TValueContainer, TJVMKey, TJVMValueContainer> : IKafkaTable
    where TKey : notnull
    where TValueContainer : class, IValueContainer<TKey>
{
    private readonly IPrincipalKeyValueFactory<TKey> _keyValueFactory;
    private readonly bool _sensitiveLoggingEnabled;
    private readonly IList<(int, ValueConverter)>? _valueConverters;
    private readonly IList<(int, ValueComparer)>? _valueComparers;

    private Dictionary<int, IKafkaIntegerValueGenerator>? _integerGenerators;
    readonly IEntityTypeProducer<TKey> _producer;
    private readonly string _tableAssociatedTopicName;
    /// <summary>
    /// Default initializer
    /// </summary>
    public KafkaTable(
        IKafkaCluster cluster,
        IEntityType entityType,
        bool sensitiveLoggingEnabled)
    {
#if DEBUG_PERFORMANCE
        Infrastructure.KafkaDbContext.ReportString($"KafkaTable Creating new KafkaTable for {entityType.Name}");
#endif
        Cluster = cluster;
        EntityType = entityType;
        _tableAssociatedTopicName = Cluster.CreateTopicForEntity(entityType);
        _producer = (IEntityTypeProducer<TKey>)EntityTypeProducers.Create<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(entityType, Cluster);
        _keyValueFactory = entityType.FindPrimaryKey()!.GetPrincipalKeyValueFactory<TKey>();
        _sensitiveLoggingEnabled = sensitiveLoggingEnabled;

        foreach (var property in entityType.GetProperties())
        {
            var converter = property.GetValueConverter()
                ?? property.FindTypeMapping()?.Converter;

            if (converter != null)
            {
                _valueConverters ??= new List<(int, ValueConverter)>();
                _valueConverters.Add((property.GetIndex(), converter));
            }

            var comparer = property.GetKeyValueComparer();
            if (!comparer.IsDefault())
            {
                _valueComparers ??= new List<(int, ValueComparer)>();
                _valueComparers.Add((property.GetIndex(), comparer));
            }
        }
    }
    /// <inheritdoc/>
    public virtual void Dispose()
    {
#if DEBUG_PERFORMANCE
        Infrastructure.KafkaDbContext.ReportString($"KafkaTable::Dispose for {EntityType.Name}");
#endif
        EntityTypeProducers.Dispose(_producer!);
    }
    /// <inheritdoc/>
    public virtual IKafkaCluster Cluster { get; }
    /// <inheritdoc/>
    public virtual IEntityType EntityType { get; }
    /// <inheritdoc/>
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

            //foreach (var table in tables)
            //{
            //    foreach (var row in table.Rows)
            //    {
            //        generator.Bump(row);
            //    }
            //}
        }

        return (KafkaIntegerValueGenerator<TProperty>)generator;
    }
    /// <inheritdoc/>
    public virtual IEnumerable<Future<RecordMetadata>> Commit(IEnumerable<IKafkaRowBag> records) => _producer.Commit(records);
    /// <inheritdoc/>
    public virtual IEnumerable<ValueBuffer> ValueBuffers => _producer.ValueBuffers;
    ///// <inheritdoc/>
    //public virtual IEnumerable<object?[]> Rows => RowsInTable();
    ///// <inheritdoc/>
    //public virtual IReadOnlyList<object?[]> SnapshotRows()
    //{
    //    var rows = Rows.ToList();
    //    var rowCount = rows.Count;
    //    var properties = EntityType.GetProperties().ToList();
    //    var propertyCount = properties.Count;

    //    for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
    //    {
    //        var snapshotRow = new object?[propertyCount];
    //        Array.Copy(rows[rowIndex], snapshotRow, propertyCount);

    //        if (_valueConverters != null)
    //        {
    //            foreach (var (index, converter) in _valueConverters)
    //            {
    //                snapshotRow[index] = converter.ConvertFromProvider(snapshotRow[index]);
    //            }
    //        }

    //        if (_valueComparers != null)
    //        {
    //            foreach (var (index, comparer) in _valueComparers)
    //            {
    //                snapshotRow[index] = comparer.Snapshot(snapshotRow[index]);
    //            }
    //        }

    //        rows[rowIndex] = snapshotRow;
    //    }

    //    return rows;
    //}

    private static List<ValueComparer> GetKeyComparers(IEnumerable<IProperty> properties) => [.. properties.Select(p => p.GetKeyValueComparer())];

    /// <inheritdoc/>
    public virtual IKafkaRowBag Create(IUpdateEntry entry)
    {
        var key = CreateKey(entry);

        if (_producer.Exist(key))
        {
            // to do: add a new string
            throw new DbUpdateConcurrencyException(KafkaStrings.UpdateConcurrencyException, [entry]);
        }
        else
        {
            var properties = entry.EntityType.GetProperties().ToArray();
            var rows = new object?[properties.Length];
            var nullabilityErrors = new List<IProperty>();

            for (var index = 0; index < properties.Length; index++)
            {
                var propertyValue = SnapshotValue(properties[index], properties[index].GetKeyValueComparer(), entry);

                rows[index] = propertyValue;
                HasNullabilityError(properties[index], propertyValue, nullabilityErrors);
            }

            if (nullabilityErrors.Count > 0)
            {
                ThrowNullabilityErrorException(entry, nullabilityErrors);
            }

            BumpValueGenerators(rows);

            return new KafkaRowBag<TKey, TValueContainer>(entry, _tableAssociatedTopicName, key, rows);
        }
    }
    /// <inheritdoc/>
    public virtual IKafkaRowBag Delete(IUpdateEntry entry)
    {
        var key = CreateKey(entry);
        if (!_producer.TryGetValue(key, out var valueBuffer))
        {
            throw new DbUpdateConcurrencyException(KafkaStrings.UpdateConcurrencyException, new[] { entry });
        }
        else
        {
            var properties = entry.EntityType.GetProperties().ToArray();
            var concurrencyConflicts = new Dictionary<IProperty, object?>();

            for (var index = 0; index < properties.Length; index++)
            {
                IsConcurrencyConflict(entry, properties[index], valueBuffer[index], concurrencyConflicts);
            }

            if (concurrencyConflicts.Count > 0)
            {
                ThrowUpdateConcurrencyException(entry, concurrencyConflicts);
            }

            return new KafkaRowBag<TKey, TValueContainer>(entry, _tableAssociatedTopicName, key, null);
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
    /// <inheritdoc/>
    public virtual IKafkaRowBag Update(IUpdateEntry entry)
    {
        var key = CreateKey(entry);

        if (!_producer.TryGetValue(key, out var data))
        {
            throw new DbUpdateConcurrencyException(KafkaStrings.UpdateConcurrencyException, new[] { entry });
        }
        else
        {
            var properties = entry.EntityType.GetProperties().ToArray();
            var comparers = GetKeyComparers(properties);
            var valueBuffer = new object?[properties.Length];
            var concurrencyConflicts = new Dictionary<IProperty, object?>();
            var nullabilityErrors = new List<IProperty>();

            for (var index = 0; index < valueBuffer.Length; index++)
            {
                if (IsConcurrencyConflict(entry, properties[index], data[index], concurrencyConflicts))
                {
                    continue;
                }

                if (HasNullabilityError(properties[index], data[index], nullabilityErrors))
                {
                    continue;
                }

                valueBuffer[index] = entry.IsModified(properties[index])
                    ? SnapshotValue(properties[index], comparers[index], entry)
                    : data[index];
            }

            if (concurrencyConflicts.Count > 0)
            {
                ThrowUpdateConcurrencyException(entry, concurrencyConflicts);
            }

            if (nullabilityErrors.Count > 0)
            {
                ThrowNullabilityErrorException(entry, nullabilityErrors);
            }

            BumpValueGenerators(valueBuffer);

            return new KafkaRowBag<TKey, TValueContainer>(entry, _tableAssociatedTopicName, key, valueBuffer);
        }
    }
    /// <inheritdoc/>
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

    private TKey CreateKey(IUpdateEntry entry) => _keyValueFactory.CreateFromCurrentValues(entry)!;

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
