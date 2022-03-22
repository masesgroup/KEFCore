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

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

public class KafkaDatabase : Database, IKafkaDatabase
{
    private readonly IKafkaCluster _cluster;
    private readonly IUpdateAdapterFactory _updateAdapterFactory;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Update> _updateLogger;
    private readonly IDesignTimeModel _designTimeModel;

    public KafkaDatabase(
        DatabaseDependencies dependencies,
        IKafkaClusterCache clusterCache,
        IDbContextOptions options,
        IDesignTimeModel designTimeModel,
        IUpdateAdapterFactory updateAdapterFactory,
        IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
        : base(dependencies)
    {
        _cluster = clusterCache.GetCluster(options);
        _designTimeModel = designTimeModel;
        _updateAdapterFactory = updateAdapterFactory;
        _updateLogger = updateLogger;
    }

    public virtual IKafkaCluster Cluster => _cluster;

    public override int SaveChanges(IList<IUpdateEntry> entries) => _cluster.ExecuteTransaction(entries, _updateLogger);

    public override Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = default)
        => cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<int>(cancellationToken)
            : Task.FromResult(_cluster.ExecuteTransaction(entries, _updateLogger));

    public virtual bool EnsureDatabaseDeleted()
        => _cluster.EnsureDeleted(_updateAdapterFactory, _designTimeModel.Model, _updateLogger);

    public virtual bool EnsureDatabaseCreated()
        => _cluster.EnsureCreated(_updateAdapterFactory, _designTimeModel.Model, _updateLogger);

    public virtual bool EnsureDatabaseConnected()
        => _cluster.EnsureConnected(_designTimeModel.Model, _updateLogger);
}
