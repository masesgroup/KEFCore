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

using MASES.EntityFrameworkCore.KNet.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;
/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public interface IKEFCoreRowBag : IValueContainerData
{
    /// <summary>
    /// The topic data will be stored
    /// </summary>
    string AssociatedTopicName { get; }
    /// <summary>
    /// The key associated to the current <see cref="IKEFCoreRowBag"/>
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    public TKey GetKey<TKey>() where TKey : notnull;
    /// <summary>
    /// The value associated to the current <see cref="IKEFCoreRowBag"/>
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValueContainer">The <see cref="IValueContainer{T}"/> containing the converted data</typeparam>
    TValueContainer? GetValue<TKey, TValueContainer>(Func<IValueContainerData, IComplexTypeConverterFactory?, TValueContainer> creator, IComplexTypeConverterFactory complexTypeConverterFactory)
        where TKey : notnull
        where TValueContainer : IValueContainer<TKey>;
}
