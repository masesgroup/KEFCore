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
using Org.Apache.Kafka.Clients.Producer;
using System.Collections;
using System.Collections.Concurrent;
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
    private readonly IKey? _primaryKey;
    private readonly IPrincipalKeyValueFactory<TKey> _keyValueFactory;
    private readonly ILoggingOptions _loggingOptions;

    readonly IEntityTypeProducer<TKey> _producer;
    private readonly string _tableAssociatedTopicName;

    private readonly ConcurrentDictionary<IEntityType, IProperty[]> _propertiesCache = new();
    private readonly ConcurrentDictionary<IEntityType, IProperty[]> _flattenedPropertiesCache = new();
    private readonly ConcurrentDictionary<IEntityType, IComplexProperty[]> _complexPropertiesCache = new();

    readonly Func<IUpdateEntry, string, TKey, IProperty[], IProperty[], object?[]?, IComplexProperty[]?, object?[]?, IKafkaRowBag> _createRowBag;

    /// <summary>
    /// Default initializer
    /// </summary>
    public KafkaTable(
        IKafkaCluster cluster,
        IEntityType entityType,
        ILoggingOptions loggingOptions)
    {
        cluster.InfrastructureLogger.Logger.LogDebug("KafkaTable Creating new KafkaTable for {Name}", entityType.Name);
        Cluster = cluster;
        _tableAssociatedTopicName = cluster.CreateTopicForEntity(entityType);
        _producer = (IEntityTypeProducer<TKey>)EntityTypeProducers.Create<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(entityType, cluster);
        _primaryKey = entityType.FindPrimaryKey();
        _keyValueFactory = _primaryKey!.GetPrincipalKeyValueFactory<TKey>();
        _loggingOptions = loggingOptions;

        var ctor = typeof(KafkaRowBag<TKey>).GetConstructors().First(); // ([typeof(IUpdateEntry), typeof(string), typeof(TKey), typeof(IProperty[]), typeof(object?[]), typeof(IComplexProperty[]), typeof(object?[])])!;
        var param1 = Expression.Parameter(typeof(IUpdateEntry));
        var param2 = Expression.Parameter(typeof(string));
        var param3 = Expression.Parameter(typeof(TKey));
        var param4 = Expression.Parameter(typeof(IProperty[]));
        var param5 = Expression.Parameter(typeof(IProperty[]));
        var param6 = Expression.Parameter(typeof(object?[]));
        var param7 = Expression.Parameter(typeof(IComplexProperty[]));
        var param8 = Expression.Parameter(typeof(object?[]));

        _createRowBag = Expression.Lambda<Func<IUpdateEntry, string, TKey, IProperty[], IProperty[], object?[]?, IComplexProperty[]?, object?[]?, IKafkaRowBag>>(
                                   Expression.New(ctor, param1, param2, param3, param4, param5, param6, param7,param8),
                                    param1, param2, param3, param4, param5, param6, param7, param8)
                        .Compile();
    }
    /// <inheritdoc/>
    public virtual void Dispose()
    {
        Cluster.InfrastructureLogger.Logger.LogDebug("KafkaTable::Dispose for {Name}", EntityType.Name);
        EntityTypeProducers.Dispose(_producer!);
    }
    /// <inheritdoc/>
    public void FindAndAddOnTracker(object[] keyValues)
    {
        _producer?.TryAddKey(keyValues);
    }

    /// <inheritdoc/>
    public virtual IKafkaCluster Cluster { get; }
    /// <inheritdoc/>
    public virtual IEntityType EntityType => _producer.EntityType;

    /// <inheritdoc/>
    public virtual void Commit(IList<Future<RecordMetadata>>? futures, IEnumerable<IKafkaRowBag> records) => _producer.Commit(futures, records);
    /// <inheritdoc/>
    public virtual IEnumerable<ValueBuffer> GetValueBuffers() => _producer.GetValueBuffers();
    /// <inheritdoc/>
    public virtual ValueBuffer? GetValueBuffer(object?[]? keyValues) => _producer.GetValueBuffer(keyValues);
    /// <inheritdoc/>
    public virtual IEnumerable<ValueBuffer> GetValueBuffersRange(object?[]? rangeStart, object?[]? rangeEnd) => _producer.GetValueBuffersRange(rangeStart, rangeEnd);
    /// <inheritdoc/>
    public virtual IEnumerable<ValueBuffer> GetValueBuffersReverse() => _producer.GetValueBuffersReverse();
    /// <inheritdoc/>
    public virtual IEnumerable<ValueBuffer> GetValueBuffersReverseRange(object?[]? rangeStart, object?[]? rangeEnd) => _producer.GetValueBuffersReverseRange(rangeStart, rangeEnd);
    /// <inheritdoc/>
    public void Start() => _producer.Start();

    private static List<ValueComparer> GetKeyComparers(IEnumerable<IProperty> properties) => [.. properties.Select(p => p.GetKeyValueComparer())];

    /// <inheritdoc/>
    public virtual IKafkaRowBag Create(IUpdateEntry entry)
    {
        var key = CreateKey(entry);

        if (_producer.Exist(key))
        {
            throw new DbUpdateConcurrencyException(KafkaStrings.DuplicateKeyException(key), [entry]);
        }
        else
        {
            var properties = _propertiesCache.GetOrAdd(entry.EntityType, (type) => [.. type.GetProperties()]);
            var flattenedProperties = _flattenedPropertiesCache.GetOrAdd(entry.EntityType, (type) => [.. type.GetFlattenedProperties()]);
            var complexProperties = _complexPropertiesCache.GetOrAdd(entry.EntityType, (type) => [.. type.GetComplexProperties()]);

            var propertyValues = new object?[flattenedProperties.Length];
            var complexPropertyValues = complexProperties != null ? new object?[complexProperties.Length] : null;
            var nullabilityErrors = new List<IProperty>();
            for (int index = 0; index < flattenedProperties.Length; index++)
            {
                var propertyValue = SnapshotValue(flattenedProperties[index], flattenedProperties[index].GetKeyValueComparer(), entry);

                propertyValues[index] = propertyValue;
                KafkaTable<TKey, TValueContainer, TJVMKey, TJVMValueContainer>.HasNullabilityError(flattenedProperties[index], propertyValue, nullabilityErrors);
            }

            if (nullabilityErrors.Count > 0)
            {
                ThrowNullabilityErrorException(entry, nullabilityErrors);
            }

            if (complexProperties != null)
            {
                for (int index = 0; index < complexProperties.Length; index++)
                {
                    complexPropertyValues![index] = entry.GetCurrentValue(complexProperties[index]);
                }
            }
            return _createRowBag(entry, _tableAssociatedTopicName, key, properties, flattenedProperties, propertyValues, complexProperties, complexPropertyValues);
        }
    }
    /// <inheritdoc/>
    public virtual IKafkaRowBag Delete(IUpdateEntry entry)
    {
        var key = CreateKey(entry);
        if (!_producer.TryGetValueBuffer(key, out var valueBuffer))
        {
            throw new DbUpdateConcurrencyException(KafkaStrings.UpdateConcurrencyException(key), [entry]);
        }
        else
        {
            var properties = _propertiesCache.GetOrAdd(entry.EntityType, (type) => [.. type.GetProperties()]);
            var flattenedProperties = _flattenedPropertiesCache.GetOrAdd(entry.EntityType, (type) => [.. type.GetFlattenedProperties()]);
            var complexProperties = _complexPropertiesCache.GetOrAdd(entry.EntityType, (type) => [.. type.GetComplexProperties()]);
            var concurrencyConflicts = new Dictionary<IProperty, object?>();

            for (var index = 0; index < flattenedProperties.Length; index++)
            {
                IsConcurrencyConflict(entry, flattenedProperties[index], valueBuffer[index], concurrencyConflicts);
            }

            if (concurrencyConflicts.Count > 0)
            {
                ThrowUpdateConcurrencyException(entry, concurrencyConflicts);
            }

            return _createRowBag(entry, _tableAssociatedTopicName, key, properties, flattenedProperties, null, complexProperties, null);
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

        if (!_producer.TryGetValueBuffer(key, out var data))
        {
            throw new DbUpdateConcurrencyException(KafkaStrings.UpdateConcurrencyException(key), new[] { entry });
        }
        else
        {
            var properties = _propertiesCache.GetOrAdd(entry.EntityType, (type) => [.. type.GetProperties()]);
            var flattenedProperties = _flattenedPropertiesCache.GetOrAdd(entry.EntityType, (type) => [.. type.GetFlattenedProperties()]);
            var complexProperties = _complexPropertiesCache.GetOrAdd(entry.EntityType, (type) => [.. type.GetComplexProperties()]);

            var comparers = GetKeyComparers(flattenedProperties);
            var propertyValues = new object?[flattenedProperties.Length];
            var complexPropertyValues = complexProperties != null ? new object?[complexProperties.Length] : null;
            var concurrencyConflicts = new Dictionary<IProperty, object?>();
            var nullabilityErrors = new List<IProperty>();
            for (int index = 0; index < flattenedProperties.Length; index++)
            {
                if (IsConcurrencyConflict(entry, flattenedProperties[index], data[index], concurrencyConflicts))
                {
                    continue;
                }

                if (KafkaTable<TKey, TValueContainer, TJVMKey, TJVMValueContainer>.HasNullabilityError(flattenedProperties[index], data[index], nullabilityErrors))
                {
                    continue;
                }

                propertyValues[index] = entry.IsModified(flattenedProperties[index])
                    ? SnapshotValue(flattenedProperties[index], comparers[index], entry)
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

            if (complexProperties != null)
            {
                for (int index = 0; index < complexProperties.Length; index++)
                {
                    complexPropertyValues![index] = entry.GetCurrentValue(complexProperties[index]); // just put current value since complex types are not tracked
                }
            }

            return _createRowBag(entry, _tableAssociatedTopicName, key, properties, flattenedProperties, propertyValues, complexProperties, complexPropertyValues);
        }
    }
    /// <inheritdoc/>
    public bool? EnsureSynchronized(long timeout)
    {
        return _producer.EnsureSynchronized(timeout);
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

    private static bool HasNullabilityError(
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
        if (_loggingOptions.IsSensitiveDataLoggingEnabled)
        {
            throw new DbUpdateException(
                KafkaStrings.NullabilityErrorExceptionSensitive(
                    nullabilityErrors.Format(),
                    entry.EntityType.DisplayName(),
                    entry.BuildCurrentValuesString(entry.EntityType.FindPrimaryKey()!.Properties)),
                [entry]);
        }

        throw new DbUpdateException(
            KafkaStrings.NullabilityErrorException(
                nullabilityErrors.Format(),
                entry.EntityType.DisplayName()),
            [entry]);
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
        if (_loggingOptions.IsSensitiveDataLoggingEnabled)
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
                [entry]);
        }

        throw new DbUpdateConcurrencyException(
            KafkaStrings.UpdateConcurrencyTokenException(
                entry.EntityType.DisplayName(),
                concurrencyConflicts.Keys.Format()),
            [entry]);
    }
}
