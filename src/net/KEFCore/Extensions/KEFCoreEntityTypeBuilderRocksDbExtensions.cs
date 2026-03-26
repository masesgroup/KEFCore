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
using MASES.KNet;
using Org.Apache.Kafka.Streams.State;

namespace MASES.EntityFrameworkCore.KNet.Extensions;

/// <summary>
/// Fluent API extensions for configuring KEFCore RocksDB lifecycle behavior on entity types.
/// </summary>
/// <remarks>
/// Two configuration models are supported:
/// <list type="bullet">
/// <item>
/// <description>
/// A handler type implementing <see cref="IRocksDbLifecycleHandler"/>.
/// </description>
/// </item>
/// <item>
/// <description>
/// Two callback handlers, one for <c>OnSetConfig</c> and one for <c>OnClose</c>,
/// internally wrapped into an <see cref="IRocksDbLifecycleHandler"/>.
/// </description>
/// </item>
/// </list>
/// </remarks>
public static class KEFCoreEntityTypeBuilderRocksDbExtensions
{
    /// <summary>
    /// Associates a RocksDB lifecycle handler type to the entity type.
    /// </summary>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <param name="handlerType">
    /// The handler type implementing <see cref="IRocksDbLifecycleHandler"/>.
    /// </param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entityTypeBuilder"/> or <paramref name="handlerType"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="handlerType"/> does not implement
    /// <see cref="IRocksDbLifecycleHandler"/>.
    /// </exception>
    public static EntityTypeBuilder HasKEFCoreRocksDbLifecycleHandler(
        this EntityTypeBuilder entityTypeBuilder,
        Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);
        ArgumentNullException.ThrowIfNull(handlerType);

        if (!typeof(IRocksDbLifecycleHandler).IsAssignableFrom(handlerType))
            throw new ArgumentException(
                $"{handlerType.Name} must implement {nameof(IRocksDbLifecycleHandler)}.",
                nameof(handlerType));

        entityTypeBuilder.Metadata.SetAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerTypeAnnotation,
            handlerType);

        entityTypeBuilder.Metadata.SetAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerAnnotation,
            null);

        return entityTypeBuilder;
    }

    /// <summary>
    /// Associates a RocksDB lifecycle handler type to the entity type.
    /// </summary>
    /// <typeparam name="THandler">
    /// The handler type implementing <see cref="IRocksDbLifecycleHandler"/>.
    /// </typeparam>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public static EntityTypeBuilder HasKEFCoreRocksDbLifecycleHandler<THandler>(
        this EntityTypeBuilder entityTypeBuilder)
        where THandler : class, IRocksDbLifecycleHandler
        => entityTypeBuilder.HasKEFCoreRocksDbLifecycleHandler(typeof(THandler));

    /// <summary>
    /// Associates a RocksDB lifecycle handler instance to the entity type.
    /// </summary>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <param name="handler">
    /// The runtime handler instance implementing <see cref="IRocksDbLifecycleHandler"/>.
    /// </param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entityTypeBuilder"/> or <paramref name="handler"/>
    /// is <see langword="null"/>.
    /// </exception>
    public static EntityTypeBuilder HasKEFCoreRocksDbLifecycleHandler(
        this EntityTypeBuilder entityTypeBuilder,
        IRocksDbLifecycleHandler handler)
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);
        ArgumentNullException.ThrowIfNull(handler);

        entityTypeBuilder.Metadata.SetAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerTypeAnnotation,
            null);

        entityTypeBuilder.Metadata.SetAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerAnnotation,
            handler);

        return entityTypeBuilder;
    }

    /// <summary>
    /// Associates RocksDB callback handlers to the entity type.
    /// </summary>
    /// <param name="entityTypeBuilder">The entity type builder.</param>
    /// <param name="onSetConfig">
    /// Callback invoked when RocksDB configures the state store.
    /// The <c>data</c> dictionary supplied to this callback is the per-store lifetime
    /// container that must retain any managed object still referenced by native
    /// RocksDB components.
    /// </param>
    /// <param name="onClose">
    /// Callback invoked when RocksDB closes the state store.
    /// The same per-store lifetime dictionary previously supplied to the
    /// <paramref name="onSetConfig"/> callback is passed back so that retained
    /// resources can be retrieved and disposed explicitly.
    /// </param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entityTypeBuilder"/> is <see langword="null"/>.
    /// </exception>
    public static EntityTypeBuilder HasKEFCoreRocksDbLifecycle(
        this EntityTypeBuilder entityTypeBuilder,
        Action<Org.Rocksdb.Options, IKNetConfigurationFromMap, IDictionary<string, object>>? onSetConfig,
        Action<Org.Rocksdb.Options, IDictionary<string, object>>? onClose)
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);

        return entityTypeBuilder.HasKEFCoreRocksDbLifecycleHandler(
            new RocksDbLifecycleDelegateHandler(onSetConfig, onClose));
    }

    /// <summary>
    /// Associates a RocksDB lifecycle handler type to the entity type.
    /// </summary>
    /// <typeparam name="TEntity">The CLR entity type.</typeparam>
    /// <typeparam name="THandler">
    /// The handler type implementing <see cref="IRocksDbLifecycleHandler"/>.
    /// </typeparam>
    /// <param name="entityTypeBuilder">The strongly typed entity type builder.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public static EntityTypeBuilder<TEntity> HasKEFCoreRocksDbLifecycleHandler<TEntity, THandler>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder)
        where TEntity : class
        where THandler : class, IRocksDbLifecycleHandler
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);

        entityTypeBuilder.Metadata.SetAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerTypeAnnotation,
            typeof(THandler));

        entityTypeBuilder.Metadata.SetAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerAnnotation,
            null);

        return entityTypeBuilder;
    }

    /// <summary>
    /// Associates a RocksDB lifecycle handler instance to the entity type.
    /// </summary>
    /// <typeparam name="TEntity">The CLR entity type.</typeparam>
    /// <param name="entityTypeBuilder">The strongly typed entity type builder.</param>
    /// <param name="handler">
    /// The runtime handler instance implementing <see cref="IRocksDbLifecycleHandler"/>.
    /// </param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public static EntityTypeBuilder<TEntity> HasKEFCoreRocksDbLifecycleHandler<TEntity>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder,
        IRocksDbLifecycleHandler handler)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);
        ArgumentNullException.ThrowIfNull(handler);

        entityTypeBuilder.Metadata.SetAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerTypeAnnotation,
            null);

        entityTypeBuilder.Metadata.SetAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerAnnotation,
            handler);

        return entityTypeBuilder;
    }

    /// <summary>
    /// Associates RocksDB callback handlers to the entity type.
    /// </summary>
    /// <typeparam name="TEntity">The CLR entity type.</typeparam>
    /// <param name="entityTypeBuilder">The strongly typed entity type builder.</param>
    /// <param name="onSetConfig">
    /// Callback invoked when RocksDB configures the state store.
    /// The <c>data</c> dictionary supplied to this callback is the per-store lifetime
    /// container that must retain any managed object still referenced by native
    /// RocksDB components.
    /// </param>
    /// <param name="onClose">
    /// Callback invoked when RocksDB closes the state store.
    /// The same per-store lifetime dictionary previously supplied to the
    /// <paramref name="onSetConfig"/> callback is passed back so that retained
    /// resources can be retrieved and disposed explicitly.
    /// </param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public static EntityTypeBuilder<TEntity> HasKEFCoreRocksDbLifecycle<TEntity>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder,
        Action<Org.Rocksdb.Options, IKNetConfigurationFromMap, IDictionary<string, object>>? onSetConfig,
        Action<Org.Rocksdb.Options, IDictionary<string, object>>? onClose)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entityTypeBuilder);

        entityTypeBuilder.Metadata.SetAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerTypeAnnotation,
            null);

        entityTypeBuilder.Metadata.SetAnnotation(
            KEFCoreAnnotationNames.RocksDbLifecycleHandlerAnnotation,
            new RocksDbLifecycleDelegateHandler(onSetConfig, onClose));

        return entityTypeBuilder;
    }
}