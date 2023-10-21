/*
*  Copyright 2023 MASES s.r.l.
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
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaDatabase : Database, IKafkaDatabase
{
    private readonly IKafkaClusterCache _clusterCache;
    private readonly IKafkaCluster _cluster;
    private readonly IUpdateAdapterFactory _updateAdapterFactory;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Update> _updateLogger;
    private readonly IDesignTimeModel _designTimeModel;
    /// <summary>
    /// Default initializer
    /// </summary>
    public KafkaDatabase(
        DatabaseDependencies dependencies,
        IKafkaClusterCache clusterCache,
        IDbContextOptions options,
        IDesignTimeModel designTimeModel,
        IUpdateAdapterFactory updateAdapterFactory,
        IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
        : base(dependencies)
    {
        _clusterCache = clusterCache;
        _cluster = _clusterCache.GetCluster(options);
        _designTimeModel = designTimeModel;
        _updateAdapterFactory = updateAdapterFactory;
        _updateLogger = updateLogger;
    }
    /// <inheritdoc/>
    public void Dispose()
    {
        _clusterCache.Dispose(_cluster);
    }
    /// <inheritdoc/>
    public virtual IKafkaCluster Cluster => _cluster;
    /// <inheritdoc/>
    public override int SaveChanges(IList<IUpdateEntry> entries) => _cluster.ExecuteTransaction(entries, _updateLogger);
    /// <inheritdoc/>
    public override Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = default)
        => cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<int>(cancellationToken)
            : Task.FromResult(_cluster.ExecuteTransaction(entries, _updateLogger));
    /// <inheritdoc/>
    public virtual bool EnsureDatabaseDeleted()
        => _cluster.EnsureDeleted(_updateAdapterFactory, _designTimeModel.Model, _updateLogger);
    /// <inheritdoc/>
    public virtual bool EnsureDatabaseCreated()
        => _cluster.EnsureCreated(_updateAdapterFactory, _designTimeModel.Model, _updateLogger);
    /// <inheritdoc/>
    public virtual bool EnsureDatabaseConnected()
        => _cluster.EnsureConnected(_designTimeModel.Model, _updateLogger);
}
