// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

using MASES.EntityFrameworkCore.KNet.Storage.Internal;

namespace MASES.EntityFrameworkCore.KNet.Query.Internal;

public class KafkaQueryContext : QueryContext
{
    private readonly IDictionary<IEntityType, IEnumerable<ValueBuffer>> _valueBuffersCache
        = new Dictionary<IEntityType, IEnumerable<ValueBuffer>>();

    public virtual IEnumerable<ValueBuffer> GetValueBuffers(IEntityType entityType)
    {
        if (!_valueBuffersCache.TryGetValue(entityType, out var valueBuffers))
        {
            //valueBuffers = Cluster
            //    .GetTables(entityType)
            //    .SelectMany(t => t.Rows.Select(vs => new ValueBuffer(vs)))
            //    .ToList();

            valueBuffers = Cluster
                .GetTables(entityType)
                .Select(vs => new ValueBuffer(vs))
                .ToList();

            _valueBuffersCache[entityType] = valueBuffers;
        }

        return valueBuffers;
    }

    public KafkaQueryContext(
        QueryContextDependencies dependencies,
        IKafkaCluster cluster)
        : base(dependencies)
    {
        Cluster = cluster;
    }

    public virtual IKafkaCluster Cluster { get; }
}
