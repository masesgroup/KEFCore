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
using Org.Apache.Kafka.Connect.Util;
using System.Collections.Concurrent;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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

    private readonly ConcurrentDictionary<(IKEFCoreCluster Cluster, string topicName), IKEFCoreTable> _factories = new();

    /// <inheritdoc/>
    public virtual IKEFCoreTable GetOrCreate(IKEFCoreDatabase database, IEntityType entityType)
        => _factories.GetOrAdd((database.Cluster, entityType.TopicName()), e => CreateTable(database, entityType)());

    /// <inheritdoc/>
    public virtual IKEFCoreTable Get(IKEFCoreCluster cluster, IEntityType entityType)
    {
        if (!_factories.TryGetValue((cluster, entityType.TopicName()), out var table))
        {
            throw new InvalidOperationException($"EntityType {entityType} with topic {entityType.TopicName()} wasn't available");
        }
        return table;
    }

    /// <summary>
    /// Allocates a new <see cref="IEntityTypeProducer"/>
    /// </summary>
    public void Start(IEnumerable<IKEFCoreTable> tables)
    {
        foreach (var table in tables)
        {
            table.Start();
        }
    }
    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var item in _factories)
        {
            item.Value.Dispose();
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
