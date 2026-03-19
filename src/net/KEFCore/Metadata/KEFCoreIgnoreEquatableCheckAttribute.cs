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
/// Suppresses the KEFCore equality check for this complex type.
/// </summary>
/// <remarks>
/// By default, KEFCore requires all complex types to implement
/// <see cref="IEquatable{T}"/> or override <see cref="object.Equals(object)"/>.
/// Apply this attribute to opt out of this check when value equality
/// is guaranteed by other means.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class KEFCoreIgnoreEquatableCheckAttribute : Attribute { }
