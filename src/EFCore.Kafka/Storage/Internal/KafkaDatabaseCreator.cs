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

public class KafkaDatabaseCreator : IDatabaseCreator
{
    private readonly IDatabase _database;

    public KafkaDatabaseCreator(IDatabase database)
    {
        _database = database;
    }

    protected virtual IKafkaDatabase Database
        => (IKafkaDatabase)_database;

    public virtual bool EnsureDeleted()
        => Database.EnsureDatabaseDeleted();

    public virtual Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Database.EnsureDatabaseDeleted());

    public virtual bool EnsureCreated()
        => Database.EnsureDatabaseCreated();

    public virtual Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Database.EnsureDatabaseCreated());

    public virtual bool CanConnect()
        => Database.EnsureDatabaseConnected();

    public virtual Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Database.EnsureDatabaseConnected());
}
