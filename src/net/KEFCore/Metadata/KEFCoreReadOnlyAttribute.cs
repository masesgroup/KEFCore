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

using MASES.EntityFrameworkCore.KNet.Infrastructure;

namespace MASES.EntityFrameworkCore.KNet.Metadata;

/// <summary>
/// Marks this entity type as read-only within the current <see cref="KEFCoreDbContext"/>.
/// Any attempt to write entries of this type via <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChanges()"/>
/// will throw <see cref="InvalidOperationException"/>.
/// </summary>
/// <remarks>
/// Unlike <see cref="KEFCoreDbContext.ReadOnlyMode"/> which applies to the entire context,
/// this attribute applies only to the decorated entity type, allowing mixed read/write models
/// where some entities are immutable reference data.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KEFCoreReadOnlyAttribute : Attribute { }
