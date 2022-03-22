/*
*  Copyright 2022 MASES s.r.l.
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
using JetBrains.Annotations;
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

public class KafkaTableFactory : IKafkaTableFactory
{
    private readonly bool _sensitiveLoggingEnabled;

    private readonly ConcurrentDictionary<(IKafkaCluster Cluster, IEntityType EntityType), Func<IKafkaTable>> _factories = new();

    public KafkaTableFactory(
        ILoggingOptions loggingOptions,
        IKafkaSingletonOptions options)
    {
        _sensitiveLoggingEnabled = loggingOptions.IsSensitiveDataLoggingEnabled;
    }

    public virtual IKafkaTable Create(IKafkaCluster cluster, IEntityType entityType)
        => _factories.GetOrAdd((cluster, entityType), e => CreateTable(e.Cluster, e.EntityType))();

    private Func<IKafkaTable> CreateTable(IKafkaCluster cluster, IEntityType entityType)
        => (Func<IKafkaTable>)typeof(KafkaTableFactory).GetTypeInfo()
            .GetDeclaredMethod(nameof(CreateFactory))!
            .MakeGenericMethod(entityType.FindPrimaryKey()!.GetKeyType())
            .Invoke(null, new object?[] { cluster, entityType, _sensitiveLoggingEnabled })!;

    private static Func<IKafkaTable> CreateFactory<TKey>(
        IKafkaCluster cluster,
        IEntityType entityType,
        bool sensitiveLoggingEnabled)
        where TKey : notnull
        => () => new KafkaTable<TKey>(cluster, entityType, sensitiveLoggingEnabled);
}
