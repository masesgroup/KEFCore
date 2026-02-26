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

namespace MASES.EntityFrameworkCore.KNet.Serialization;

/// <summary>
/// The interface shall be implemented and used from any external manager which neeeds to interact with serialization sub-system to managed <see cref="IComplexProperty"/>
/// </summary>
/// <remarks>The implementation shall be thread-safe and the class shall have at least a default initializer</remarks>
public interface IComplexTypeConverter
{
    /// <summary>
    /// The set of <see cref="Type"/> supported from the converter
    /// </summary>
    IEnumerable<Type> SupportedClrTypes { get; }
    /// <summary>
    /// The method is used from constructor of <see cref="IValueContainer{T}"/> to manage <see cref="IComplexProperty"/> that does not have an autonomous conversion
    /// </summary>
    /// <param name="data">The input data coming from EF Core to be converted, the converted value shall return on the same reference</param>
    /// <returns><see langword="true"/> if the <see cref="IComplexTypeConverter"/> has converted the <paramref name="data"/>, if it not able to execute the conversion, or want to leave the management to the subsystem, shall return <see langword="false"/></returns>
    /// <remarks>The <see cref="MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage.DefaultValueContainer{TKey}"/> by default uses POCO serialization so it does not need to implement the method and can return <see langword="false"/>.</remarks>
    bool Convert(ref object? data);
    /// <summary>
    /// The method is used from <see cref="IValueContainer{T}.GetData(IEntityType, IProperty[], IComplexProperty[], ref object[], IComplexTypeConverterFactory?)"/> and <see cref="IValueContainer{T}.GetProperties"/> to manage <see cref="IComplexProperty"/> that does not have an autonomous conversion
    /// </summary>
    /// <param name="data">The input data coming from serialization to be converted back, the converted value shall return on the same reference</param>
    /// <returns><see langword="true"/> if the <see cref="IComplexTypeConverter"/> has converted the <paramref name="data"/>, if it not able to execute the conversion shall return <see langword="false"/></returns>
    /// <remarks>The <see cref="MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage.DefaultValueContainer{TKey}"/> by default uses POCO serialization so it does not need to implement the method and can return <see langword="false"/>.</remarks>
    bool ConvertBack(ref object? data);
}
