/*
*  Copyright 2022 MASES s.r.l.
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

using MASES.EntityFrameworkCore.Kafka.Infrastructure.Internal;
using MASES.KafkaBridge.Streams;
using System.ComponentModel;

namespace MASES.EntityFrameworkCore.Kafka.Infrastructure;

/// <summary>
///     Allows Kafka specific configuration to be performed on <see cref="DbContextOptions" />.
/// </summary>
/// <remarks>
///     <para>
///         Instances of this class are returned from a call to
///         <see
///             cref="KafkaDbContextOptionsExtensions.UseKafkaDatabase(DbContextOptionsBuilder, string, System.Action{KafkaDbContextOptionsBuilder})" />
///         and it is not designed to be directly constructed in your application code.
///     </para>
///     <para>
///         See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
///         <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and examples.
///     </para>
/// </remarks>
public class KafkaDbContextOptionsBuilder : IKafkaDbContextOptionsBuilderInfrastructure
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="KafkaDbContextOptionsBuilder" /> class.
    /// </summary>
    /// <param name="optionsBuilder">The options builder.</param>
    public KafkaDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
    {
        OptionsBuilder = optionsBuilder;
    }

    /// <summary>
    ///     Clones the configuration in this builder.
    /// </summary>
    /// <returns>The cloned configuration.</returns>
    protected virtual DbContextOptionsBuilder OptionsBuilder { get; }

    /// <inheritdoc />
    DbContextOptionsBuilder IKafkaDbContextOptionsBuilderInfrastructure.OptionsBuilder
        => OptionsBuilder;

    /// <summary>
    ///     Enables nullability check for all properties across all entities within the Kafka database.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see>, and
    ///     <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and examples.
    /// </remarks>
    /// <param name="nullChecksEnabled">If <see langword="true" />, then nullability check is enforced.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual KafkaDbContextOptionsBuilder AutoOffsetReset(Topology.AutoOffsetReset autoOffsetReset = Topology.AutoOffsetReset.EARLIEST)
    {
        var extension = OptionsBuilder.Options.FindExtension<KafkaOptionsExtension>()
            ?? new KafkaOptionsExtension();

        extension = extension.WithAutoOffsetReset(autoOffsetReset);

        ((IDbContextOptionsBuilderInfrastructure)OptionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    #region Hidden System.Object members

    /// <summary>
    ///     Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override string? ToString()
        => base.ToString();

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns><see langword="true" /> if the specified object is equal to the current object; otherwise, <see langword="false" />.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals(object? obj)
        => base.Equals(obj);

    /// <summary>
    ///     Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override int GetHashCode()
        => base.GetHashCode();

    #endregion
}
