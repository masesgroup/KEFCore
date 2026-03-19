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

using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KEFCoreDatabase : Database, IKEFCoreDatabase
{
    private readonly IKEFCoreClusterCache _clusterCache;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Infrastructure> _infrastructureLogger;
    private readonly IValueGeneratorSelector _valueGeneratorSelector;
    private readonly KEFCoreOptionsExtension _options;
    private readonly IDesignTimeModel _designTimeModel;
    private readonly IUpdateAdapterFactory _updateAdapterFactory;
    private readonly IKEFCoreCluster _cluster;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Update> _updateLogger;
    /// <summary>
    /// Default initializer
    /// </summary>
    public KEFCoreDatabase(
        DatabaseDependencies dependencies,
        IKEFCoreClusterCache clusterCache,
        IDiagnosticsLogger<DbLoggerCategory.Infrastructure> infrastructureLogger,
        IValueGeneratorSelector valueGeneratorSelector,
        IDbContextOptions options,
        IDesignTimeModel designTimeModel,
        IUpdateAdapterFactory updateAdapterFactory,
        IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
        : base(dependencies)
    {
        _clusterCache = clusterCache;
        _infrastructureLogger = infrastructureLogger;
        _valueGeneratorSelector = valueGeneratorSelector;
        _options = options.Extensions.OfType<KEFCoreOptionsExtension>().First();
        _designTimeModel = designTimeModel;
        _updateAdapterFactory = updateAdapterFactory;
        _updateLogger = updateLogger;
        _cluster = _clusterCache.CreateCluster(_options);
        _cluster.Register(this);
    }
    /// <inheritdoc/>
    public void Dispose()
    {
        _cluster.Unregister(this);
    }
    /// <inheritdoc/>
    public virtual IKEFCoreCluster Cluster => _cluster;
    /// <inheritdoc/>
    public virtual IDiagnosticsLogger<DbLoggerCategory.Infrastructure> InfrastructureLogger => _infrastructureLogger;
    /// <inheritdoc/>
    public virtual IValueGeneratorSelector ValueGeneratorSelector => _valueGeneratorSelector;
    /// <inheritdoc/>
    public virtual KEFCoreOptionsExtension Options => _options;
    /// <inheritdoc/>
    public virtual IDesignTimeModel DesignTimeModel => _designTimeModel;
    /// <inheritdoc/>
    public virtual IUpdateAdapterFactory UpdateAdapterFactory => _updateAdapterFactory;
    /// <inheritdoc/>
    public virtual IList<IKEFCoreTable> Tables { get; } = [];
    /// <inheritdoc/>
    public override int SaveChanges(IList<IUpdateEntry> entries) => _cluster.ExecuteTransaction(this, entries, _updateLogger);
    /// <inheritdoc/>
    public override Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = default) 
        => cancellationToken.IsCancellationRequested ? Task.FromCanceled<int>(cancellationToken)
                                                     : _cluster.ExecuteTransactionAsync(this, entries, _updateLogger, cancellationToken);
    /// <inheritdoc/>
    public virtual bool EnsureDatabaseDeleted() => _cluster.EnsureDeleted(this, _updateLogger);
    /// <inheritdoc/>
    public virtual bool EnsureDatabaseCreated() => _cluster.EnsureCreated(this, _updateLogger);
    /// <inheritdoc/>
    public virtual bool EnsureDatabaseConnected() => _cluster.EnsureConnected(this, _updateLogger);
    /// <inheritdoc/>
    public virtual bool? EnsureDatabaseSynchronized(long timeout) => _cluster.EnsureSynchronized(this, timeout);
}
