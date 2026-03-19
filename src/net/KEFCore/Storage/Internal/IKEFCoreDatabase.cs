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
public interface IKEFCoreDatabase : IDatabase, IDisposable
{
    /// <summary>
    /// The referring <see cref="IKEFCoreCluster"/>
    /// </summary>
    IKEFCoreCluster Cluster { get; }
    /// <summary>
    /// Reference to <see cref="IDiagnosticsLogger{TLoggerCategory}"/> received
    /// </summary>
    IDiagnosticsLogger<DbLoggerCategory.Infrastructure> InfrastructureLogger { get; }
    /// <summary>
    /// Reference to <see cref="IValueGeneratorSelector"/> received
    /// </summary>
    IValueGeneratorSelector ValueGeneratorSelector { get; }
    /// <summary>
    /// Reference to <see cref="KEFCoreOptionsExtension"/> received
    /// </summary>
    KEFCoreOptionsExtension Options { get; }
    /// <summary>
    /// Reference to <see cref="IDesignTimeModel"/> received
    /// </summary>
    IDesignTimeModel DesignTimeModel { get; }
    /// <summary>
    /// Reference to <see cref="IUpdateAdapterFactory"/> received
    /// </summary>
    IUpdateAdapterFactory UpdateAdapterFactory { get; }
    /// <summary>
    /// Reference to the set of <see cref="IKEFCoreTable"/> associated
    /// </summary>
    IList<IKEFCoreTable> Tables { get; }
    /// <summary>
    /// Execute the <see cref="IDatabaseCreator.EnsureDeleted"/>
    /// </summary>
    bool EnsureDatabaseDeleted();
    /// <summary>
    /// Execute the <see cref="IDatabaseCreator.EnsureCreated"/>
    /// </summary>
    bool EnsureDatabaseCreated();
    /// <summary>
    /// Execute the <see cref="IDatabaseCreator.CanConnect"/>
    /// </summary>
    bool EnsureDatabaseConnected();
    /// <summary>
    /// Verify if local instance is synchronized with the <see cref="Cluster"/>
    /// </summary>
    bool? EnsureDatabaseSynchronized(long timeout);
}