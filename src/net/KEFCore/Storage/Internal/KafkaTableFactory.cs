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

using System.Collections.Concurrent;
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.EntityFrameworkCore.KNet.Serialization;

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
public class KafkaTableFactory(
    ILoggingOptions loggingOptions,
    IKafkaSingletonOptions options) : IKafkaTableFactory
{
    private readonly ILoggingOptions _loggingOptions = loggingOptions;
    private readonly IKafkaSingletonOptions _options = options;

    private readonly ConcurrentDictionary<(IKafkaCluster Cluster, Type EntityType), IKafkaTable> _factories = new();

    /// <inheritdoc/>
    public virtual IKafkaTable Create(IKafkaCluster cluster, IEntityType entityType)
        => _factories.GetOrAdd((cluster, entityType.ClrType), e => CreateTable(cluster, entityType)());

    /// <inheritdoc/>
    public virtual IKafkaTable Get(IKafkaCluster cluster, IEntityType entityType)
    {
        if (!_factories.TryGetValue((cluster, entityType.ClrType), out var table))
        {
            throw new InvalidOperationException($"{entityType} on ClusterId {cluster.ClusterId} not registered yet.");
        }
        return table;
    }

    /// <inheritdoc/>
    public void Dispose(IKafkaTable table)
    {
        if (table != null)
        {
            table.Dispose();
            _factories.TryRemove((table.Cluster, table.EntityType.ClrType), out _);
        }
    }

    private Func<IKafkaTable> CreateTable(IKafkaCluster Cluster, IEntityType EntityType)
        => (Func<IKafkaTable>)typeof(KafkaTableFactory).GetTypeInfo()
            .GetDeclaredMethod(nameof(CreateFactory))!
            .MakeGenericMethod(EntityType.FindPrimaryKey()!.GetKeyType(),
                               _options.ValueContainerType(EntityType),
                               _options.JVMKeyType(EntityType),
                               _options.JVMValueContainerType(EntityType))
            .Invoke(null, [Cluster, EntityType, _loggingOptions])!;

    private static Func<IKafkaTable> CreateFactory<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(
        IKafkaCluster Cluster,
        IEntityType EntityType,
        ILoggingOptions loggingOptions)
        where TKey : notnull
        where TValueContainer : class, IValueContainer<TKey>
        => () => new KafkaTable<TKey, TValueContainer, TJVMKey, TJVMValueContainer>(Cluster, EntityType, loggingOptions);
}
