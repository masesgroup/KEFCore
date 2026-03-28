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

using MASES.EntityFrameworkCore.KNet.Metadata.Internal;
using Org.Apache.Kafka.Streams.State;

namespace MASES.EntityFrameworkCore.KNet.Extensions;

/// <summary>
/// Extension methods for KEFCore RocksDB lifecycle metadata associated to entity types.
/// </summary>
public static class KEFCoreEntityTypeRocksDbExtensions
{
    /// <summary>
    /// Gets the RocksDB lifecycle handler type associated to the entity type.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns>
    /// The handler type implementing <see cref="IRocksDbLifecycleHandler"/>,
    /// or <see langword="null"/> if no handler type was configured.
    /// </returns>
    public static Type? GetRocksDbLifecycleHandlerType(this IReadOnlyEntityType entityType)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.RocksDbLifecycleHandlerTypeAnnotation)?.Value as Type;

    /// <summary>
    /// Sets the RocksDB lifecycle handler type associated to the entity type.
    /// </summary>
    /// <param name="entityType">The mutable entity type.</param>
    /// <param name="handlerType">
    /// The handler type implementing <see cref="IRocksDbLifecycleHandler"/>,
    /// or <see langword="null"/> to remove the annotation.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="handlerType"/> does not implement
    /// <see cref="IRocksDbLifecycleHandler"/>.
    /// </exception>
    public static void SetRocksDbLifecycleHandlerType(this IMutableEntityType entityType, Type? handlerType)
    {
        if (handlerType is not null &&
            !typeof(IRocksDbLifecycleHandler).IsAssignableFrom(handlerType))
        {
            throw new ArgumentException(
                $"{handlerType.Name} must implement {nameof(IRocksDbLifecycleHandler)}.",
                nameof(handlerType));
        }

        entityType.SetOrRemoveAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerTypeAnnotation,
            handlerType);
    }

    /// <summary>
    /// Sets the RocksDB lifecycle handler type associated to the entity type.
    /// </summary>
    /// <param name="entityType">The convention entity type.</param>
    /// <param name="handlerType">
    /// The handler type implementing <see cref="IRocksDbLifecycleHandler"/>,
    /// or <see langword="null"/> to remove the annotation.
    /// </param>
    /// <param name="fromDataAnnotation">
    /// Indicates whether the configuration was specified using a data annotation.
    /// </param>
    /// <returns>The configured handler type.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="handlerType"/> does not implement
    /// <see cref="IRocksDbLifecycleHandler"/>.
    /// </exception>
    public static Type? SetRocksDbLifecycleHandlerType(
        this IConventionEntityType entityType,
        Type? handlerType,
        bool fromDataAnnotation = false)
    {
        if (handlerType is not null &&
            !typeof(IRocksDbLifecycleHandler).IsAssignableFrom(handlerType))
        {
            throw new ArgumentException(
                $"{handlerType.Name} must implement {nameof(IRocksDbLifecycleHandler)}.",
                nameof(handlerType));
        }

        return entityType.SetOrRemoveAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerTypeAnnotation,
            handlerType,
            fromDataAnnotation)?.Value as Type;
    }

    /// <summary>
    /// Gets the RocksDB lifecycle handler instance associated to the entity type.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns>
    /// The configured handler instance, or <see langword="null"/> if no handler instance was configured.
    /// </returns>
    public static IRocksDbLifecycleHandler? GetRocksDbLifecycleHandler(this IReadOnlyEntityType entityType)
        => entityType.FindAnnotation(KEFCoreAnnotationNames.RocksDbLifecycleHandlerAnnotation)?.Value as IRocksDbLifecycleHandler;

    /// <summary>
    /// Sets the RocksDB lifecycle handler instance associated to the entity type.
    /// </summary>
    /// <param name="entityType">The mutable entity type.</param>
    /// <param name="handler">
    /// The handler instance implementing <see cref="IRocksDbLifecycleHandler"/>,
    /// or <see langword="null"/> to remove the annotation.
    /// </param>
    public static void SetRocksDbLifecycleHandler(
        this IMutableEntityType entityType,
        IRocksDbLifecycleHandler? handler)
        => entityType.SetOrRemoveAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerAnnotation,
            handler);

    /// <summary>
    /// Sets the RocksDB lifecycle handler instance associated to the entity type.
    /// </summary>
    /// <param name="entityType">The convention entity type.</param>
    /// <param name="handler">
    /// The handler instance implementing <see cref="IRocksDbLifecycleHandler"/>,
    /// or <see langword="null"/> to remove the annotation.
    /// </param>
    /// <param name="fromDataAnnotation">
    /// Indicates whether the configuration was specified using a data annotation.
    /// </param>
    /// <returns>The configured handler instance.</returns>
    public static IRocksDbLifecycleHandler? SetRocksDbLifecycleHandler(
        this IConventionEntityType entityType,
        IRocksDbLifecycleHandler? handler,
        bool fromDataAnnotation = false)
        => entityType.SetOrRemoveAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerAnnotation,
            handler,
            fromDataAnnotation)?.Value as IRocksDbLifecycleHandler;
}