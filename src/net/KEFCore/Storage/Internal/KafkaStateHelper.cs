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

using MASES.EntityFrameworkCore.KNet.Serialization;
using MASES.EntityFrameworkCore.KNet.ValueGeneration.Internal;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal
{
    internal class KafkaStateHelper
    {
        public static void ManageAdded<TKey, TValueContainer>(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, IValueGeneratorSelector valueGeneratorSelector, IComplexTypeConverterFactory converterFactory, IUpdateAdapterFactory factory, IEntityType entityType, IKey ikey, TKey key, TValueContainer container)
            where TKey : notnull
            where TValueContainer : IValueContainer<TKey>
        {
            var adapter = factory.Create();
            if (key is not object?[] keyValues)
            {
                keyValues = [key];
            }

            if (container is null) 
            {
                logger.Logger.LogDebug("Record with key {key} removed (tombstone) try delete", key);
                // maybe a record removed (tombstone) and it is still in kafka, try a delete
                ManageDeleteInternal(logger, adapter, ikey, keyValues);
            } 
            else
            {
                logger.Logger.LogDebug("Record with key {key} added", key);
                ManageAddedInternal(logger, valueGeneratorSelector, adapter, entityType, ikey, keyValues, container.GetProperties(converterFactory));
            }
        }

        public static void ManageAdded<TKey, TValueContainer>(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, IValueGeneratorSelector valueGeneratorSelector, IComplexTypeConverterFactory converterFactory, IUpdateAdapter adapter, IEntityType entityType, IKey ikey, TKey key, TValueContainer container)
            where TKey : notnull
            where TValueContainer : IValueContainer<TKey>
        {
            if (container is null) return; // maybe a record removed (tombstone) that it is still in kafka

            if (key is not object?[] keyValues)
            {
                keyValues = [key];
            }

            ManageAddedInternal(logger, valueGeneratorSelector, adapter, entityType, ikey, keyValues, container.GetProperties(converterFactory));
        }

        static void ManageAddedInternal(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, IValueGeneratorSelector valueGeneratorSelector, IUpdateAdapter adapter, IEntityType entityType, IKey ikey, object?[] keyValues, IDictionary<string, object?> propertyValues)
        {
            IUpdateEntry? entry = adapter.TryGetEntry(ikey, keyValues);
            if (entry == null)
            {
                logger.Logger.LogDebug("ManageAddedInternal: Record not available, adding");
                var properties = entityType.GetValueGeneratingProperties();
                foreach (var item in properties)
                {
#if NET9_0 || NET10_0
                    if (!valueGeneratorSelector.TrySelect(item, entityType, out ValueGenerator? valueGenerator)) valueGenerator = null;
#else
                    ValueGenerator? valueGenerator = valueGeneratorSelector?.Select(item, entityType);
#endif
                    if (valueGenerator is IKafkaValueGenerator iKafkaGenerator)
                    {
                        iKafkaGenerator.Bump(keyValues);
                    }
                }
                // the key does not exist
                var newEntry = adapter.CreateEntry(propertyValues, entityType);
                newEntry.EntityState = EntityState.Unchanged;
                //adapter.DetectChanges();
            }
            else
            {
                logger.Logger.LogDebug("ManageAddedInternal: Record available, try update");
                ManageUpdateInternal(logger, valueGeneratorSelector, entry, propertyValues);
            }
        }

        public static void ManageUpdate<TKey, TValueContainer>(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, IValueGeneratorSelector valueGeneratorSelector, IComplexTypeConverterFactory converterFactory, IUpdateAdapterFactory factory, IEntityType entityType, IKey ikey, TKey key, TValueContainer container)
            where TKey : notnull
            where TValueContainer : class, IValueContainer<TKey>
        {
            if (container is null) return; // maybe a record removed (tombstone) that it is still in kafka

            if (key is not object?[] keyValues)
            {
                keyValues = [key];
            }

            var adapter = factory.Create();
            IUpdateEntry? entry = adapter.TryGetEntry(ikey, keyValues);
            var properties = container.GetProperties(converterFactory);
            if (entry != null)
            {
                logger.Logger.LogDebug("ManageUpdate: Record with key {key} exist, update", key);
                ManageUpdateInternal(logger, valueGeneratorSelector, entry, properties);
            }
            else
            {
                logger.Logger.LogDebug("ManageUpdate: Record with key {key} does not exists, add", key);
                ManageAddedInternal(logger, valueGeneratorSelector, adapter, entityType, ikey, keyValues, properties);
            }
        }

        public static void ManageUpdate<TKey, TValueContainer>(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, IValueGeneratorSelector valueGeneratorSelector, IComplexTypeConverterFactory converterFactory, IUpdateAdapter adapter, IEntityType entityType, IKey ikey, TKey key, TValueContainer container)
            where TKey : notnull
            where TValueContainer : class, IValueContainer<TKey>
        {
            if (container is null) return; // maybe a record removed (tombstone) that it is still in kafka

            if (key is not object?[] keyValues)
            {
                keyValues = [key];
            }

            IUpdateEntry? entry = adapter.TryGetEntry(ikey, keyValues);
            var properties = container.GetProperties(converterFactory);
            if (entry != null)
            {
                logger.Logger.LogDebug("ManageUpdate: Record exists, update");
                ManageUpdateInternal(logger, valueGeneratorSelector, entry, properties);
            }
            else
            {
                logger.Logger.LogDebug("ManageUpdate: Record does not exists, add");
                ManageAddedInternal(logger, valueGeneratorSelector, adapter, entityType, ikey, keyValues, properties);
            }
        }

        public static void ManageUpdateInternal(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, IValueGeneratorSelector valueGeneratorSelector, IUpdateEntry entry, IDictionary<string, object?> propertyValues)
        {
            bool changed = false;
            foreach (var item in propertyValues)
            {
                var prop = entry.EntityType.FindProperty(item.Key!);
                if (prop == null) continue; // a property was removed from the schema
                var currentValue = entry.GetCurrentValue(prop);
                if (!Equals(item.Value, currentValue)) // if received data introduced a null value while current value is not null or received data is different from current value
                {
                    changed = true;
                    entry.SetOriginalValue(prop, item.Value);
                }
            }
            logger.Logger.LogDebug("ManageUpdateInternal: Record changed={changed}", changed);
            if (changed)
            {
                entry.EntityState = EntityState.Modified;
                //adapter.DetectChanges();
            }
        }

        public static void ManageDelete<TKey>(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, IUpdateAdapterFactory factory, IKey ikey, TKey key)
            where TKey : notnull
        {
            if (key is not object?[] keyValues)
            {
                keyValues = [key];
            }
            logger.Logger.LogDebug("ManageDelete: Record {key} try delete", key);
            var adapter = factory.Create();
            ManageDeleteInternal(logger, adapter, ikey, keyValues);
        }

        public static void ManageDelete<TKey>(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, IUpdateAdapter adapter, IKey ikey, TKey key)
            where TKey : notnull
        {
            if (key is not object?[] keyValues)
            {
                keyValues = [key];
            }

            ManageDeleteInternal(logger, adapter, ikey, keyValues);
        }

        public static void ManageDeleteInternal(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, IUpdateAdapter adapter, IKey ikey, object?[] keyValues)
        {
            IUpdateEntry? entry = adapter.TryGetEntry(ikey, keyValues);
            if (entry != null && entry.EntityState != EntityState.Deleted)
            {
                logger.Logger.LogDebug("ManageDeleteInternal: Record exists, delete with cascade");
                adapter.CascadeDelete(entry);
                //adapter.DetectChanges();
            }
        }

        public static void ManageFind(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, IUpdateAdapterFactory factory, IEntityType entityType, IKey key, object?[] keyValues, IDictionary<string, object?>? propertyValues)
        {
            var adapter = factory.Create();
            IUpdateEntry? entry = adapter.TryGetEntry(key, keyValues);
            if (entry == null)
            {
                logger.Logger.LogDebug("ManageFind: Record does not exist exists, add in state as Unchanged");
                var entity2 = adapter.Model.FindEntityType(entityType.ClrType);
                // the key does not exist
                var newEntry = adapter.CreateEntry(propertyValues!, entity2!);
                newEntry.EntityState = EntityState.Unchanged;
                //adapter.DetectChanges();
            }
        }
    }
}
