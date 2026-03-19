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

namespace MASES.EntityFrameworkCore.KNet.Metadata;

/// <summary>
/// Disables KEFCore event management for this entity type,
/// overriding the context-level default set by <c>UseKEFCoreManageEvents()</c>.
/// </summary>
/// <remarks>
/// By default, event management is enabled for all entity types.
/// Apply this attribute to opt out of real-time tracking updates
/// via <c>TimestampExtractor</c> for a specific entity.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KEFCoreIgnoreEventsAttribute : Attribute { }
