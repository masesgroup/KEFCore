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

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class KEFCoreTransaction : IDbContextTransaction
{
    private IKEFCoreCluster? _cluster;
    private List<string>? _activeGroups;
    volatile int _disposed; // 0 = live, 1 = disposed

    /// <inheritdoc/>
    public virtual Guid TransactionId { get; } = Guid.NewGuid();

    /// <summary>
    /// Called by ExecuteTransaction after PrepareTransaction to register the active groups
    /// and call BeginTransaction() on each transactional producer.
    /// </summary>
    internal void Begin(IEnumerable<string> transactionGroups, IKEFCoreCluster cluster)
    {
        _cluster = cluster;
        _activeGroups = transactionGroups.Distinct().ToList();
        foreach (var group in _activeGroups)
            _cluster.BeginTransactions(group);
    }

    /// <summary>Returns true if this is a real transaction (not the stub).</summary>
    internal bool IsActive => _cluster != null;
    /// <inheritdoc/>
    public virtual void Commit()
    {
        if (_activeGroups == null) return;
        foreach (var group in _activeGroups)
        {
            _cluster!.CommitTransactions(group);
        }
        _activeGroups = null;
    }
    /// <inheritdoc/>
    public virtual async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_activeGroups == null) return;
        foreach (var group in _activeGroups)
        {
            await Task.Run(() => _cluster!.CommitTransactions(group), cancellationToken);
        }
        _activeGroups = null;
    }
    /// <inheritdoc/>
    public virtual void Rollback()
    {
        if (_activeGroups == null) return;
        foreach (var group in _activeGroups)
        {
            _cluster!.AbortTransactions(group);
        }
        _activeGroups = null;
    }
    /// <inheritdoc/>
    public virtual async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_activeGroups == null) return;
        foreach (var group in _activeGroups)
        {
            await Task.Run(() => _cluster!.AbortTransactions(group), cancellationToken);
        }
        _activeGroups = null;
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public virtual void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(true);
        // Suppress finalization.
        GC.SuppressFinalize(this);
    }
    /// <summary>
    /// Implements the pattern described in https://learn.microsoft.com/en-en/dotnet/standard/garbage-collection/implementing-dispose
    /// </summary>
    /// <param name="disposing">The disposing parameter is a <see langword="bool"/> that indicates whether the method call comes from a <see cref="IDisposable.Dispose"/> method (its value is <see langword="true"/>) or from a finalizer (its value is <see langword="false"/>)</param>
    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (disposing)
        {

        }
    }

    /// <inheritdoc/>
    public virtual ValueTask DisposeAsync() => default;
}
