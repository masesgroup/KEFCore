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

using MASES.EntityFrameworkCore.KNet.Storage.Internal;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Internal;

namespace MASES.EntityFrameworkCore.KNet.Internal
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Not found any other way to override the find behavior from a provider.")]
    internal class KafkaEntityFinder<TEntity> : IEntityFinder<TEntity> where TEntity : class
    {
        readonly IEntityFinder _internalEntityFinder;
        readonly IEntityFinder<TEntity> _internalEntityFinderTEntity; // used to propagate search on original one without override it

        private readonly IStateManager _stateManager;
        private readonly IDbSetSource _setSource;
        private readonly IDbSetCache _setCache;
        private readonly IEntityType _entityType;
        private readonly IKafkaTableFactory _kafkaTableFactory;
        private readonly IKafkaClusterCache _kafkaClusterCache;
        private readonly IKafkaCluster _cluster;
        private readonly IKafkaTableEntityFinder _kafkaTableEntityFinder;
        private readonly IKey _primaryKey;
        private readonly Type _primaryKeyType;
        private readonly int _primaryKeyPropertiesCount;

        public KafkaEntityFinder(IStateManager stateManager, IDbSetSource setSource, IDbSetCache setCache, IEntityType entityType,
                                 IKafkaTableFactory kafkaTableFactory, IKafkaClusterCache kafkaClusterCache)
        {
            _internalEntityFinderTEntity = new EntityFinder<TEntity>(stateManager, setSource, setCache, entityType);
            _internalEntityFinder = _internalEntityFinderTEntity as IEntityFinder;
            _stateManager = stateManager;
            _setSource = setSource;
            _setCache = setCache;
            _entityType = entityType;
            _kafkaTableFactory = kafkaTableFactory;
            _kafkaClusterCache = kafkaClusterCache;

            IDbContextOptions options = ((DbContext)_setCache).GetService<IDbContextOptions>();
            _cluster = _kafkaClusterCache.GetCluster(options);
            _kafkaTableEntityFinder = _kafkaTableFactory.Get(_cluster, entityType) as IKafkaTableEntityFinder;

            _primaryKey = entityType.FindPrimaryKey()!;
            _primaryKeyPropertiesCount = _primaryKey.Properties.Count;
            _primaryKeyType = _primaryKeyPropertiesCount == 1 ? _primaryKey.Properties[0].ClrType : typeof(IReadOnlyList<object?>);
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual TEntity? Find(object?[]? keyValues)
        {
            if (keyValues == null
                || keyValues.Any(v => v == null))
            {
                return default;
            }

            var (processedKeyValues, _) = ValidateKeyPropertiesAndExtractCancellationToken(keyValues!, async: false, default);

            return FindTracked(processedKeyValues) ?? FindLocal(processedKeyValues);
        }

        private TEntity? FindLocal(object[] keyValues)
        {
            _kafkaTableEntityFinder.FindAndAddOnTracker(keyValues);
            return _internalEntityFinderTEntity.Find(keyValues);
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        object? IEntityFinder.Find(object?[]? keyValues)
            => Find(keyValues);

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual InternalEntityEntry? FindEntry<TKey>(TKey keyValue) 
            => _internalEntityFinder.FindEntry(keyValue);

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual InternalEntityEntry? FindEntry<TProperty>(IProperty property, TProperty propertyValue) 
            => _internalEntityFinder.FindEntry(property, propertyValue);

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual IEnumerable<InternalEntityEntry> GetEntries<TProperty>(IProperty property, TProperty propertyValue) =>
            _internalEntityFinder.GetEntries(property, propertyValue);
        
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual InternalEntityEntry? FindEntry(IEnumerable<object?> keyValues) =>         
            _internalEntityFinder.FindEntry(keyValues);

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual InternalEntityEntry? FindEntry(IEnumerable<IProperty> properties, IEnumerable<object?> propertyValues) 
            => _internalEntityFinder.FindEntry(properties, propertyValues);

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual IEnumerable<InternalEntityEntry> GetEntries(IEnumerable<IProperty> properties, IEnumerable<object?> propertyValues) 
            => _internalEntityFinder.GetEntries(properties, propertyValues);

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual ValueTask<TEntity?> FindAsync(object?[]? keyValues, CancellationToken cancellationToken = default)
        {
            if (keyValues == null
                || keyValues.Any(v => v == null))
            {
                return default;
            }

            var (processedKeyValues, ct) = ValidateKeyPropertiesAndExtractCancellationToken(keyValues!, async: true, cancellationToken);

            var tracked = FindTracked(processedKeyValues);
            return tracked != null
                ? new ValueTask<TEntity?>(tracked)
                : FindAsyncLocal(processedKeyValues, ct);
        }

        ValueTask<TEntity?> FindAsyncLocal(object[] processedKeyValues, CancellationToken cancellationToken = default)
        {
            _kafkaTableEntityFinder.FindAndAddOnTracker(processedKeyValues);
            return _internalEntityFinderTEntity.FindAsync(processedKeyValues, cancellationToken);
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        ValueTask<object?> IEntityFinder.FindAsync(object?[]? keyValues, CancellationToken cancellationToken)
        {
            if (keyValues == null
                || keyValues.Any(v => v == null))
            {
                return default;
            }

            var (processedKeyValues, ct) = ValidateKeyPropertiesAndExtractCancellationToken(keyValues!, async: true, cancellationToken);

            var tracked = FindTracked(processedKeyValues);
            return tracked != null
                ? new ValueTask<object?>(tracked)
                : FindAsyncLocalIEntityFinder(processedKeyValues, cancellationToken);
        }

        ValueTask<object?> FindAsyncLocalIEntityFinder(object[] processedKeyValues, CancellationToken cancellationToken = default)
        {
            _kafkaTableEntityFinder.FindAndAddOnTracker(processedKeyValues);
            return _internalEntityFinder.FindAsync(processedKeyValues, cancellationToken);
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual void Load(INavigation navigation, InternalEntityEntry entry, LoadOptions options) => _internalEntityFinder.Load(navigation, entry, options);

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual async Task LoadAsync(INavigation navigation, InternalEntityEntry entry, LoadOptions options, CancellationToken ct = default) 
            => await _internalEntityFinder.LoadAsync(navigation, entry, options, ct);

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual IQueryable<TEntity> Query(INavigation navigation, InternalEntityEntry entry) 
            => _internalEntityFinderTEntity.Query(navigation, entry);

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual object[]? GetDatabaseValues(InternalEntityEntry entry) 
            => _internalEntityFinder.GetDatabaseValues(entry);

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual Task<object[]?> GetDatabaseValuesAsync(InternalEntityEntry entry, CancellationToken ct = default) 
            => _internalEntityFinder.GetDatabaseValuesAsync(entry, ct);

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        IQueryable IEntityFinder.Query(INavigation navigation, InternalEntityEntry entry) 
            => _internalEntityFinder.Query(navigation, entry);

        private (object[] KeyValues, CancellationToken CancellationToken) ValidateKeyPropertiesAndExtractCancellationToken(
            object[] keyValues,
            bool async,
            CancellationToken cancellationToken)
        {
            if (_primaryKeyPropertiesCount != keyValues.Length)
            {
                if (async
                    && _primaryKeyPropertiesCount == keyValues.Length - 1
                    && keyValues[_primaryKeyPropertiesCount] is CancellationToken ct)
                {
                    var newValues = new object[_primaryKeyPropertiesCount];
                    Array.Copy(keyValues, newValues, _primaryKeyPropertiesCount);
                    return (newValues, ct);
                }

                if (_primaryKeyPropertiesCount == 1)
                {
                    throw new ArgumentException(
                        CoreStrings.FindNotCompositeKey(typeof(TEntity).ShortDisplayName(), keyValues.Length));
                }

                throw new ArgumentException(
                    CoreStrings.FindValueCountMismatch(typeof(TEntity).ShortDisplayName(), _primaryKeyPropertiesCount, keyValues.Length));
            }

            return (keyValues, cancellationToken);
        }

        private TEntity? FindTracked(object[] keyValues)
        {
            var keyProperties = _primaryKey.Properties;
            for (var i = 0; i < keyValues.Length; i++)
            {
                var valueType = keyValues[i].GetType();
                var propertyType = keyProperties[i].ClrType;
                if (!propertyType.UnwrapNullableType().IsAssignableFrom(valueType))
                {
                    throw new ArgumentException(
                        CoreStrings.FindValueTypeMismatch(
                            i, typeof(TEntity).ShortDisplayName(), valueType.ShortDisplayName(), propertyType.ShortDisplayName()));
                }
            }

            return _stateManager.TryGetEntry(_primaryKey, keyValues)?.Entity as TEntity;
        }
    }
}
