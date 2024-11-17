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
/// <param name="database"></param>
public class KafkaDatabaseCreator(IDatabase database) : IDatabaseCreator
{
    private readonly IDatabase _database = database;

    /// <summary>
    /// The <see cref="IKafkaDatabase"/>
    /// </summary>
    protected virtual IKafkaDatabase Database
        => (IKafkaDatabase)_database;
    /// <inheritdoc/>
    public virtual bool EnsureDeleted()
        => Database.EnsureDatabaseDeleted();
    /// <inheritdoc/>
    public virtual Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Database.EnsureDatabaseDeleted());
    /// <inheritdoc/>
    public virtual bool EnsureCreated()
        => Database.EnsureDatabaseCreated();
    /// <inheritdoc/>
    public virtual Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Database.EnsureDatabaseCreated());
    /// <inheritdoc/>
    public virtual bool CanConnect()
        => Database.EnsureDatabaseConnected();
    /// <inheritdoc/>
    public virtual Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Database.EnsureDatabaseConnected());
}
