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

using MASES.EntityFrameworkCore.KNet.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal
{
    internal class KafkaStateHelper
    {
        public static void ManageAdded<TKey, TValueContainer>(IUpdateAdapterFactory factory, IEntityType entityType, IKey ikey, TKey key, TValueContainer container)
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
                // maybe a record removed (tombstone) and it is still in kafka, try a delete
                ManageDeleteInternal(adapter, ikey, keyValues);
            } 
            else
            {
                ManageAddedInternal(adapter, entityType, ikey, keyValues, container.GetProperties());
            }
        }

        public static void ManageAdded<TKey, TValueContainer>(IUpdateAdapter adapter, IEntityType entityType, IKey ikey, TKey key, TValueContainer container)
            where TKey : notnull
            where TValueContainer : IValueContainer<TKey>
        {
            if (container is null) return; // maybe a record removed (tombstone) that it is still in kafka

            if (key is not object?[] keyValues)
            {
                keyValues = [key];
            }

            ManageAddedInternal(adapter, entityType, ikey, keyValues, container.GetProperties());
        }

        static void ManageAddedInternal(IUpdateAdapter adapter, IEntityType entityType, IKey ikey, object?[] keyValues, IDictionary<string, object?> properties)
        {
            var entity2 = adapter.Model.FindEntityType(entityType.ClrType);
            var _primaryKey = entity2!.FindPrimaryKey();
            IUpdateEntry? entry = adapter.TryGetEntry(_primaryKey!, keyValues);
            if (entry == null)
            {
                // the key does not exist
                // var entity2 = adapter.Model.FindEntityType(entityType.ClrType);
                var newEntry = adapter.CreateEntry(properties, entity2!);
                newEntry.EntityState = EntityState.Unchanged;
                //adapter.DetectChanges();
            }
            else
            {
                ManageUpdateInternal(entry, properties);
            }
        }

        public static void ManageUpdate<TKey, TValueContainer>(IUpdateAdapterFactory factory, IEntityType entityType, IKey ikey, TKey key, TValueContainer container)
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
            var properties = container.GetProperties();
            if (entry != null)
            {
                ManageUpdateInternal(entry, properties);
            }
            else
            {
                ManageAddedInternal(adapter, entityType, ikey, keyValues, properties);
            }
        }

        public static void ManageUpdate<TKey, TValueContainer>(IUpdateAdapter adapter, IEntityType entityType, IKey ikey, TKey key, TValueContainer container)
            where TKey : notnull
            where TValueContainer : class, IValueContainer<TKey>
        {
            if (container is null) return; // maybe a record removed (tombstone) that it is still in kafka

            if (key is not object?[] keyValues)
            {
                keyValues = [key];
            }

            IUpdateEntry? entry = adapter.TryGetEntry(ikey, keyValues);
            var properties = container.GetProperties();
            if (entry != null)
            {
                ManageUpdateInternal(entry, properties);
            }
            else
            {
                ManageAddedInternal(adapter, entityType, ikey, keyValues, properties);
            }
        }

        public static void ManageUpdateInternal(IUpdateEntry entry, IDictionary<string, object?> properties)
        {
            bool changed = false;
            foreach (var item in properties)
            {
                var prop = entry.EntityType.FindProperty(item.Key!);
                if (prop == null) continue; // a property was removed from the schema
                var currentValue = entry.GetCurrentValue(prop);
                if (!item.Value!.Equals(currentValue))
                {
                    changed = true;
                    entry.SetOriginalValue(prop, item.Value);
                }
            }

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
                adapter.CascadeDelete(entry);
                //adapter.DetectChanges();
            }
        }

        public static void ManageFind(IUpdateAdapterFactory factory, IEntityType entityType, IKey key, object?[] keyValues, IDictionary<string, object?>? properties)
        {
            var adapter = factory.Create();
            IUpdateEntry? entry = adapter.TryGetEntry(key, keyValues);
            if (entry == null)
            {
                var entity2 = adapter.Model.FindEntityType(entityType.ClrType);
                // the key does not exist
                var newEntry = adapter.CreateEntry(properties!, entity2!);
                newEntry.EntityState = EntityState.Unchanged;
                //adapter.DetectChanges();
            }
        }
    }
}
