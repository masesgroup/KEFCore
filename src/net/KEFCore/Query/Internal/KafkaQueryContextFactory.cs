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

public class KafkaQueryContextFactory : IQueryContextFactory
{
    private readonly IKafkaCluster _cluster;
    readonly KafkaQueryContext context;

    public KafkaQueryContextFactory(
        QueryContextDependencies dependencies,
        IKafkaClusterCache clusterCache,
        IDbContextOptions contextOptions)
    {
        _cluster = clusterCache.GetCluster(contextOptions);
        Dependencies = dependencies;
        context = new KafkaQueryContext(Dependencies, _cluster);
    }

    /// <summary>
    ///     Dependencies for this service.
    /// </summary>
    protected virtual QueryContextDependencies Dependencies { get; }

    public virtual QueryContext Create() => context; // new KafkaQueryContext(Dependencies, _cluster);
}
