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
using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;
using MASES.EntityFrameworkCore.KNet.Serdes.Internal;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

public class KafkaClusterCache : IKafkaClusterCache
{
    private readonly IKafkaTableFactory _tableFactory;
    private readonly IKafkaSerdesFactory _serdesFactory;
    private readonly ConcurrentDictionary<string, IKafkaCluster> _namedClusters;

    public KafkaClusterCache(
        IKafkaTableFactory tableFactory,
        IKafkaSerdesFactory serdesFactory,
        IKafkaSingletonOptions? options)
    {
        _tableFactory = tableFactory;
        _serdesFactory = serdesFactory;
        _namedClusters = new ConcurrentDictionary<string, IKafkaCluster>();
    }

    public virtual IKafkaCluster GetCluster(KafkaOptionsExtension options)
        => _namedClusters.GetOrAdd(options.ClusterId, _ => new KafkaCluster(options, _tableFactory, _serdesFactory));
}
