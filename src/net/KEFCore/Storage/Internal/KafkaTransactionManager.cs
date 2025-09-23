/*
*  Copyright 2022 - 2025 MASES s.r.l.
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
    /// <summary>
    /// Default initializer
    /// </summary>
    public KafkaTransactionManager(
        IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger)
    {
        _logger = logger;
    }
    /// <inheritdoc/>
    public virtual IDbContextTransaction BeginTransaction()
    {
        _logger.TransactionIgnoredWarning();

        return StubTransaction;
    }
    /// <inheritdoc/>
    public virtual Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.TransactionIgnoredWarning();

        return Task.FromResult<IDbContextTransaction>(StubTransaction);
    }
    /// <inheritdoc/>
    public virtual void CommitTransaction()
        => _logger.TransactionIgnoredWarning();
    /// <inheritdoc/>
    public virtual Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        _logger.TransactionIgnoredWarning();
        return Task.CompletedTask;
    }
    /// <inheritdoc/>
    public virtual void RollbackTransaction()
        => _logger.TransactionIgnoredWarning();
    /// <inheritdoc/>
    public virtual Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        _logger.TransactionIgnoredWarning();
        return Task.CompletedTask;
    }
    /// <inheritdoc/>
    public virtual IDbContextTransaction? CurrentTransaction
        => null;
    /// <inheritdoc/>
    public virtual Transaction? EnlistedTransaction
        => null;
    /// <inheritdoc/>
    public virtual void EnlistTransaction(Transaction? transaction)
        => _logger.TransactionIgnoredWarning();
    /// <inheritdoc/>
    public virtual void ResetState()
    {
    }
    /// <inheritdoc/>
    public virtual Task ResetStateAsync(CancellationToken cancellationToken = default)
    {
        ResetState();

        return Task.CompletedTask;
    }
}
