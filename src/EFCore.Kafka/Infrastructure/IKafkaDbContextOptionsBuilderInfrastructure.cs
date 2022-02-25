﻿// Licensed to the .NET Foundation under one or more agreements.
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

namespace MASES.EntityFrameworkCore.Kafka.Infrastructure;

/// <summary>
///     Explicitly implemented by <see cref="KafkaDbContextOptionsBuilder" /> to hide
///     methods that are used by database provider extension methods but not intended to be called by application
///     developers.
/// </summary>
public interface IKafkaDbContextOptionsBuilderInfrastructure
{
    /// <summary>
    ///     Gets the core options builder.
    /// </summary>
    DbContextOptionsBuilder OptionsBuilder { get; }
}
