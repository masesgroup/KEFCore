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

using MASES.EntityFrameworkCore.KNet.Extensions;
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.EntityFrameworkCore.KNet.Serialization;
using System.Collections.Concurrent;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
/// Default initializer
/// </remarks>
public class KEFCoreTableFactory(
    ILoggingOptions loggingOptions,
    IKEFCoreSingletonOptions options) : IKEFCoreTableFactory
{
    private readonly ILoggingOptions _loggingOptions = loggingOptions;
    private readonly IKEFCoreSingletonOptions _options = options;
    volatile int _disposed; // 0 = live, 1 = disposed

    private readonly ConcurrentDictionary<(IKEFCoreCluster Cluster, string topicName), IKEFCoreTable> _factories = new();

    /// <inheritdoc/>
    public virtual IKEFCoreTable GetOrCreate(IKEFCoreDatabase database, IEntityType entityType)
    {
        if (!_factories.TryGetValue((database.Cluster, entityType.GetKEFCoreTopicName()), out var table))
        {
            table = CreateTable(database, entityType)();
            if (!_factories.TryAdd((database.Cluster, entityType.GetKEFCoreTopicName()), table))
            {
                _factories.TryGetValue((database.Cluster, entityType.GetKEFCoreTopicName()), out table);
            }
        }
        return table!;
    }

    /// <inheritdoc/>
    public virtual IKEFCoreTable Get(IKEFCoreCluster cluster, IEntityType entityType)
    {
        if (!_factories.TryGetValue((cluster, entityType.GetKEFCoreTopicName()), out var table))
        {
            throw new InvalidOperationException($"EntityType {entityType} with topic {entityType.GetKEFCoreTopicName()} wasn't available");
        }
        return table;
    }

    /// <inheritdoc/>
    public virtual bool NeedsNewTables(IKEFCoreCluster cluster, IEnumerable<IEntityType> entityTypes)
    {
        bool result = true;
        foreach (var item in entityTypes)
        {
            result &= _factories.ContainsKey((cluster, item.GetKEFCoreTopicName()));
        }
        return !result;
    }

    /// <inheritdoc/>
    public void Start(IKEFCoreDatabase database)
    {
        foreach (var table in database.Tables)
        {
            table.Start(database);
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(true);
        // Suppress finalization.
        GC.SuppressFinalize(this);
    }
    /// <summary>
    /// Implements the pattern described in https://learn.microsoft.com/en-en/dotnet/standard/garbage-collection/implementing-dispose
    /// </summary>
    /// <param name="disposing">The disposing parameter is a <see langword="bool"/> that indicates whether the method call comes from a <see cref="IDisposable.Dispose"/> method (its value is <see langword="true"/>) or from a finalizer (its value is <see langword="false"/>)</param>
    void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (disposing)
        {
            foreach (var item in _factories)
            {
                item.Value.Dispose();
            }
        }
    }

    private Func<IKEFCoreTable> CreateTable(IKEFCoreDatabase database, IEntityType entityType)
        => (Func<IKEFCoreTable>)typeof(KEFCoreTableFactory).GetTypeInfo()
            .GetDeclaredMethod(nameof(CreateFactory))!
            .MakeGenericMethod(entityType.FindPrimaryKey()!.GetKeyType(),
                               _options.ValueContainerType(entityType),
                               _options.JVMKeyType(entityType),
                               _options.JVMValueContainerType(entityType))
            .Invoke(null, [database, entityType, _loggingOptions])!;

    private static Func<IKEFCoreTable> CreateFactory<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(
        IKEFCoreDatabase database,
        IEntityType entityType,
        ILoggingOptions loggingOptions)
        where TKey : notnull
        where TValueContainer : class, IValueContainer<TKey>
        => () => new KEFCoreTable<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(database, entityType, loggingOptions);
}
