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

#nullable enable

using Org.Apache.Kafka.Streams.State;

namespace MASES.EntityFrameworkCore.KNet.Metadata;

/// <summary>
/// Declares the <see cref="IRocksDbLifecycleHandler"/> type associated to an entity type.
/// </summary>
/// <remarks>
/// This attribute supports only a handler <see cref="Type"/> because attributes cannot store
/// runtime delegates or runtime handler instances.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class KEFCoreRocksDbLifecycleAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="KEFCoreRocksDbLifecycleAttribute"/>.
    /// </summary>
    /// <param name="handlerType">
    /// The handler type implementing <see cref="IRocksDbLifecycleHandler"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="handlerType"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="handlerType"/> does not implement
    /// <see cref="IRocksDbLifecycleHandler"/>.
    /// </exception>
    public KEFCoreRocksDbLifecycleAttribute(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);

        if (!typeof(IRocksDbLifecycleHandler).IsAssignableFrom(handlerType))
            throw new ArgumentException(
                $"{handlerType.Name} must implement {nameof(IRocksDbLifecycleHandler)}.",
                nameof(handlerType));

        HandlerType = handlerType;
    }

    /// <summary>
    /// Gets the handler type implementing <see cref="IRocksDbLifecycleHandler"/>.
    /// </summary>
    public Type HandlerType { get; }
}