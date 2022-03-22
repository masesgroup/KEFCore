// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

using MASES.EntityFrameworkCore.KNet.Infrastructure.Internal;

namespace MASES.EntityFrameworkCore.KNet;

/// <summary>
///     Kafka specific extension methods for <see cref="DbContext.Database" />.
/// </summary>
public static class KafkaDatabaseFacadeExtensions
{
    /// <summary>
    ///     Returns <see langword="true" /> if the database provider currently in use is the Kafka provider.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method can only be used after the <see cref="DbContext" /> has been configured because
    ///         it is only then that the provider is known. This means that this method cannot be used
    ///         in <see cref="DbContext.OnConfiguring" /> because this is where application code sets the
    ///         provider to use as part of configuring the context.
    ///     </para>
    ///     <para>
    ///         See <see href="https://github.com/masesgroup/EntityFramework4Kafka">The EF Core Kafka database provider</see> for more information and
    ///         examples.
    ///     </para>
    /// </remarks>
    /// <param name="database">The facade from <see cref="DbContext.Database" />.</param>
    /// <returns><see langword="true" /> if the Kafka database is being used.</returns>
    public static bool IsKafka(this DatabaseFacade database)
        => database.ProviderName == typeof(KafkaOptionsExtension).Assembly.GetName().Name;
}
