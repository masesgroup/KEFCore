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

using MASES.EntityFrameworkCore.KNet.Infrastructure;

namespace MASES.EntityFrameworkCore.KNet.Storage;
/// <summary>
/// The event data informing about changes on an <see cref="IEntityType"/> using the <see cref="KafkaDbContext.OnChangeEvent"/> event handler
/// </summary>
public readonly struct EntityTypeChanged
{
    /// <summary>
    /// The change occurred
    /// </summary>
    [Flags]
    public enum ChangeKindType
    {
        /// <summary>
        /// The <see cref="Key"/> was added
        /// </summary>
        Added = 1,
        /// <summary>
        /// The <see cref="Key"/> was updated
        /// </summary>
        Updated = 2,
        /// <summary>
        /// The <see cref="Key"/> was removed
        /// </summary>
        Removed = 4,
    }

    internal EntityTypeChanged(IEntityType entityType, ChangeKindType changeKind, object key)
    {
        EntityType = entityType;
        ChangeKind = changeKind;
        Key = key;
    }
    /// <summary>
    /// The <see cref="IEntityType"/> with changes
    /// </summary>
    public IEntityType EntityType { get; }
    /// <summary>
    /// The <see cref="ChangeKindType.Removed"/> if <see cref="Key"/> was deleted, otherwise <see cref="Key"/> was added or updated
    /// </summary>
    public ChangeKindType ChangeKind { get; }
    /// <summary>
    /// The key removed if <see cref="ChangeKind"/> is <see cref="ChangeKindType.Removed"/>, otherwise it was added or updated
    /// </summary>
    public object? Key { get; }
    /// <summary>
    /// Helper to understand if the <see cref="Key"/> was added
    /// </summary>
    public bool KeyAdded => ChangeKind.HasFlag(ChangeKindType.Added);
    /// <summary>
    /// Helper to understand if the <see cref="Key"/> was updated
    /// </summary>
    public bool KeyUpdated => ChangeKind.HasFlag(ChangeKindType.Updated);
    /// <summary>
    /// Helper to understand if the <see cref="Key"/> was removed
    /// </summary>
    public bool KeyRemoved => ChangeKind.HasFlag(ChangeKindType.Removed);
    /// <summary>
    /// Helper to understand if the <see cref="Key"/> was added or updated
    /// </summary>
    public bool KeyUpserted => ChangeKind.HasFlag(ChangeKindType.Added) | ChangeKind.HasFlag(ChangeKindType.Updated);
}
