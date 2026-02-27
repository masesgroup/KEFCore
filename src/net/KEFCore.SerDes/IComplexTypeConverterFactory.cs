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
public interface IComplexTypeConverterFactory
{
    /// <summary>
    /// Allocate and register all <see cref="Type"/> in an <see cref="Assembly"/> managing an instance of <see cref="IComplexTypeConverter"/>
    /// </summary>
    /// <param name="convertersAssembly">The <see cref="Assembly"/> containg the <see cref="Type"/> will be used from <see cref="Register(Type)"/></param>
    public void Register(Assembly convertersAssembly);
    /// <summary>
    /// Allocate and register a <see cref="Type"/> managing an instance of <see cref="IComplexTypeConverter"/>
    /// </summary>
    /// <param name="converterType">The <see cref="Type"/> containing the <see cref="IComplexTypeConverter"/> will be used from <see cref="Register(IComplexTypeConverter)"/></param>
    public void Register(Type converterType);
    /// <summary>
    /// Register a <see cref="IComplexTypeConverter"/>
    /// </summary>
    /// <param name="converter">The <see cref="IComplexTypeConverter"/> with all <see cref="Type"/> it manages (see <see cref="IComplexTypeConverter.SupportedClrTypes"/>)</param>
    public void Register(IComplexTypeConverter converter);

    /// <summary>
    /// The method is used from <see cref="IValueContainer{T}"/> to retrieve <see cref="IComplexTypeConverter"/> associated to <paramref name="complexProperty"/>
    /// </summary>
    /// <param name="complexProperty">The <see cref="IComplexProperty"/> to be converted</param>
    /// <param name="converter">The <see cref="IComplexTypeConverter"/> associated to <paramref name="complexProperty"/></param>
    /// <returns><see langword="true"/> if the <see cref="IComplexTypeConverter"/> was found otherwise <see langword="false"/></returns>
    bool TryGet(IPropertyBase? complexProperty, out IComplexTypeConverter? converter);
    /// <summary>
    /// The method is used from constructor of <see cref="IValueContainer{T}"/> to retrieve <see cref="IComplexTypeConverter"/> associated to <paramref name="complexPropertyType"/>
    /// </summary>
    /// <param name="complexPropertyType">The CLR <see cref="Type"/> of the <see cref="IComplexProperty"/> to be converted</param>
    /// <param name="converter">The <see cref="IComplexTypeConverter"/> associated to <paramref name="complexPropertyType"/></param>
    /// <returns><see langword="true"/> if the <see cref="IComplexTypeConverter"/> was found otherwise <see langword="false"/></returns>
    bool TryGet(Type complexPropertyType, out IComplexTypeConverter? converter);
    /// <summary>
    /// The method is used from constructor of <see cref="IValueContainer{T}"/> to retrieve <see cref="IComplexTypeConverter"/> associated to <paramref name="complexPropertyType"/>
    /// </summary>
    /// <param name="complexPropertyType">The CLR <see cref="Type"/> of the <see cref="IComplexProperty"/> to be converted</param>
    /// <param name="converter">The <see cref="IComplexTypeConverter"/> associated to <paramref name="complexPropertyType"/></param>
    /// <returns><see langword="true"/> if the <see cref="IComplexTypeConverter"/> was found otherwise <see langword="false"/></returns>
    bool TryGet(string complexPropertyType, out IComplexTypeConverter? converter);
}
