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

using System.Transactions;
using MASES.EntityFrameworkCore.KNet.Diagnostics.Internal;
using MASES.EntityFrameworkCore.KNet.Internal;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KafkaTransactionManager : IDbContextTransactionManager, ITransactionEnlistmentManager
{
    private static readonly KafkaTransaction StubTransaction = new();

    private readonly IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> _logger;

    public KafkaTransactionManager(
        IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger)
    {
        _logger = logger;
    }

    public virtual IDbContextTransaction BeginTransaction()
    {
        _logger.TransactionIgnoredWarning();

        return StubTransaction;
    }

    public virtual Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.TransactionIgnoredWarning();

        return Task.FromResult<IDbContextTransaction>(StubTransaction);
    }

    public virtual void CommitTransaction()
        => _logger.TransactionIgnoredWarning();

    public virtual Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        _logger.TransactionIgnoredWarning();
        return Task.CompletedTask;
    }

    public virtual void RollbackTransaction()
        => _logger.TransactionIgnoredWarning();

    public virtual Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        _logger.TransactionIgnoredWarning();
        return Task.CompletedTask;
    }

    public virtual IDbContextTransaction? CurrentTransaction
        => null;

    public virtual Transaction? EnlistedTransaction
        => null;

    public virtual void EnlistTransaction(Transaction? transaction)
        => _logger.TransactionIgnoredWarning();

    public virtual void ResetState()
    {
    }

    public virtual Task ResetStateAsync(CancellationToken cancellationToken = default)
    {
        ResetState();

        return Task.CompletedTask;
    }
}
