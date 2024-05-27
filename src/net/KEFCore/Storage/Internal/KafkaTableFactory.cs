/*
*  Copyright 2024 MASES s.r.l.
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
using MASES.KNet.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaTableFactory : IKafkaTableFactory
{
    private readonly IKafkaSingletonOptions _options;
    private readonly bool _sensitiveLoggingEnabled;

    private readonly ConcurrentDictionary<(IKafkaCluster Cluster, IEntityType EntityType), Func<IKafkaTable>> _factories = new();
    /// <summary>
    /// Default initializer
    /// </summary>
    public KafkaTableFactory(
        ILoggingOptions loggingOptions,
        IKafkaSingletonOptions options)
    {
        _options = options;
        _sensitiveLoggingEnabled = loggingOptions.IsSensitiveDataLoggingEnabled;
    }
    /// <inheritdoc/>
    public virtual IKafkaTable Create(IKafkaCluster cluster, IEntityType entityType)
        => _factories.GetOrAdd((cluster, entityType), e => CreateTable(e.Cluster, e.EntityType))();
    /// <inheritdoc/>
    public void Dispose(IKafkaTable table)
    {
        table.Dispose();
    }

    private Func<IKafkaTable> CreateTable(IKafkaCluster cluster, IEntityType entityType)
        => (Func<IKafkaTable>)typeof(KafkaTableFactory).GetTypeInfo()
            .GetDeclaredMethod(nameof(CreateFactory))!
            .MakeGenericMethod(entityType.FindPrimaryKey()!.GetKeyType(), 
                               _options.ValueContainerType(entityType),
                               _options.JVMKeyType(entityType),
                               _options.JVMValueContainerType(entityType),
                               _options.SerDesSelectorTypeForKey(entityType), 
                               _options.SerDesSelectorTypeForValue(entityType))
            .Invoke(null, new object?[] { cluster, entityType, _sensitiveLoggingEnabled })!;

    private static Func<IKafkaTable> CreateFactory<TKey, TValueContainer, TJVMKey, TJVMValueContainer, TKeySerDesSelectorType, TValueContainerSerDesSelectorType>(
        IKafkaCluster cluster,
        IEntityType entityType,
        bool sensitiveLoggingEnabled)
        where TKey : notnull
        where TValueContainer : class, IValueContainer<TKey>
        where TKeySerDesSelectorType : class, ISerDesSelector<TKey>, new()
        where TValueContainerSerDesSelectorType : class, ISerDesSelector<TValueContainer>, new()
        => () => new KafkaTable<TKey, TValueContainer, TJVMKey, TJVMValueContainer, TKeySerDesSelectorType, TValueContainerSerDesSelectorType>(cluster, entityType, sensitiveLoggingEnabled);
}
