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
        public static void ManageAdded<TKey, TValueContainer>(IValueGeneratorSelector valueGeneratorSelector, IComplexTypeConverterFactory converterFactory, IUpdateAdapterFactory factory, IEntityType entityType, IKey ikey, TKey key, TValueContainer container)
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
#if DEBUG_PERFORMANCE
                KNet.Internal.DebugPerformanceHelper.ReportString($"Record with key {key} removed (tombstone) try delete");
#endif
                // maybe a record removed (tombstone) and it is still in kafka, try a delete
                ManageDeleteInternal(adapter, ikey, keyValues);
            } 
            else
            {
#if DEBUG_PERFORMANCE
                KNet.Internal.DebugPerformanceHelper.ReportString($"Record with key {key} added");
#endif
                ManageAddedInternal(valueGeneratorSelector, adapter, entityType, ikey, keyValues, container.GetProperties(converterFactory));
            }
        }

        public static void ManageAdded<TKey, TValueContainer>(IValueGeneratorSelector valueGeneratorSelector, IComplexTypeConverterFactory converterFactory, IUpdateAdapter adapter, IEntityType entityType, IKey ikey, TKey key, TValueContainer container)
            where TKey : notnull
            where TValueContainer : IValueContainer<TKey>
        {
            if (container is null) return; // maybe a record removed (tombstone) that it is still in kafka

            if (key is not object?[] keyValues)
            {
                keyValues = [key];
            }

            ManageAddedInternal(valueGeneratorSelector, adapter, entityType, ikey, keyValues, container.GetProperties(converterFactory));
        }

        static void ManageAddedInternal(IValueGeneratorSelector valueGeneratorSelector, IUpdateAdapter adapter, IEntityType entityType, IKey ikey, object?[] keyValues, IDictionary<string, object?> propertyValues)
        {
            IUpdateEntry? entry = adapter.TryGetEntry(ikey, keyValues);
            if (entry == null)
            {
#if DEBUG_PERFORMANCE
                KNet.Internal.DebugPerformanceHelper.ReportString($"ManageAddedInternal: Record not available, adding");
#endif
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
#if DEBUG_PERFORMANCE
                KNet.Internal.DebugPerformanceHelper.ReportString($"ManageAddedInternal: Record available, try update");
#endif
                ManageUpdateInternal(valueGeneratorSelector, entry, propertyValues);
            }
        }

        public static void ManageUpdate<TKey, TValueContainer>(IValueGeneratorSelector valueGeneratorSelector, IComplexTypeConverterFactory converterFactory, IUpdateAdapterFactory factory, IEntityType entityType, IKey ikey, TKey key, TValueContainer container)
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
#if DEBUG_PERFORMANCE
                KNet.Internal.DebugPerformanceHelper.ReportString($"ManageUpdate: Record with key {key} exist, update");
#endif
                ManageUpdateInternal(valueGeneratorSelector, entry, properties);
            }
            else
            {
#if DEBUG_PERFORMANCE
                KNet.Internal.DebugPerformanceHelper.ReportString($"ManageUpdate: Record with key {key} does not exists, add");
#endif
                ManageAddedInternal(valueGeneratorSelector, adapter, entityType, ikey, keyValues, properties);
            }
        }

        public static void ManageUpdate<TKey, TValueContainer>(IValueGeneratorSelector valueGeneratorSelector, IComplexTypeConverterFactory converterFactory, IUpdateAdapter adapter, IEntityType entityType, IKey ikey, TKey key, TValueContainer container)
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
#if DEBUG_PERFORMANCE
                KNet.Internal.DebugPerformanceHelper.ReportString($"ManageUpdate: Record exists, update");
#endif
                ManageUpdateInternal(valueGeneratorSelector, entry, properties);
            }
            else
            {
#if DEBUG_PERFORMANCE
                KNet.Internal.DebugPerformanceHelper.ReportString($"ManageUpdate: Record does not exists, add");
#endif
                ManageAddedInternal(valueGeneratorSelector, adapter, entityType, ikey, keyValues, properties);
            }
        }

        public static void ManageUpdateInternal(IValueGeneratorSelector valueGeneratorSelector, IUpdateEntry entry, IDictionary<string, object?> propertyValues)
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
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"ManageUpdateInternal: Record changed={changed}");
#endif
            if (changed)
            {
                entry.EntityState = EntityState.Modified;
                //adapter.DetectChanges();
            }
        }

        public static void ManageDelete<TKey>(IUpdateAdapterFactory factory, IKey ikey, TKey key)
            where TKey : notnull
        {
            if (key is not object?[] keyValues)
            {
                keyValues = [key];
            }
#if DEBUG_PERFORMANCE
            KNet.Internal.DebugPerformanceHelper.ReportString($"ManageDelete: Record {key} try delete");
#endif
            var adapter = factory.Create();
            ManageDeleteInternal(adapter, ikey, keyValues);
        }

        public static void ManageDelete<TKey>(IUpdateAdapter adapter, IKey ikey, TKey key)
            where TKey : notnull
        {
            if (key is not object?[] keyValues)
            {
                keyValues = [key];
            }

            ManageDeleteInternal(adapter, ikey, keyValues);
        }

        public static void ManageDeleteInternal(IUpdateAdapter adapter, IKey ikey, object?[] keyValues)
        {
            IUpdateEntry? entry = adapter.TryGetEntry(ikey, keyValues);
            if (entry != null && entry.EntityState != EntityState.Deleted)
            {
#if DEBUG_PERFORMANCE
                KNet.Internal.DebugPerformanceHelper.ReportString($"ManageDeleteInternal: Record exists, delete with cascade");
#endif
                adapter.CascadeDelete(entry);
                //adapter.DetectChanges();
            }
        }

        public static void ManageFind(IUpdateAdapterFactory factory, IEntityType entityType, IKey key, object?[] keyValues, IDictionary<string, object?>? propertyValues)
        {
            var adapter = factory.Create();
            IUpdateEntry? entry = adapter.TryGetEntry(key, keyValues);
            if (entry == null)
            {
#if DEBUG_PERFORMANCE
                KNet.Internal.DebugPerformanceHelper.ReportString($"ManageFind: Record does not exist exists, add in state as Unchanged");
#endif
                var entity2 = adapter.Model.FindEntityType(entityType.ClrType);
                // the key does not exist
                var newEntry = adapter.CreateEntry(propertyValues!, entity2!);
                newEntry.EntityState = EntityState.Unchanged;
                //adapter.DetectChanges();
            }
        }
    }
}
