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
/// <see cref="PreferredConversionType"/> defines the preferred conversion the underlying serializer expected
/// </summary>
/// <remarks>The value is not strictly mandatory, however it defines if the underlying serializer is <see cref="Text"/> (maybe Json) or <see cref="Binary"/> (Avro/Protobuf)</remarks>
public enum PreferredConversionType
{
    /// <summary>
    /// The preferred conversion expect a <see cref="string"/>, generally requested from text based serializers like Json
    /// </summary>
    Text,
    /// <summary>
    /// The preferred conversion expect a <see cref="byte"/> array, generally requested from binary based serializers like AVRO/Protobuf
    /// </summary>
    Binary
}

/// <summary>
/// The interface adds support to <see cref="IComplexTypeConverter"/> for logging behavior
/// </summary>
public interface IComplexTypeConverterLogging
{
    /// <summary>
    /// <see cref="IDiagnosticsLogger{TLoggerCategory}"/> can be used to give back information
    /// </summary>
    IDiagnosticsLogger<DbLoggerCategory.Infrastructure> Logging { get; }
    /// <summary>
    /// Register an <see cref="IDiagnosticsLogger{TLoggerCategory}"/> instance can be used to give back information
    /// </summary>
    /// <param name="logging"><see cref="IDiagnosticsLogger{TLoggerCategory}"/> can be used to give back information</param>
    /// <remarks>This method is invoked only once to register the <paramref name="logging"/> without the need of a specific costructor</remarks>
    void Register(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logging);
}

/// <summary>
/// The interface shall be implemented and used from any external converter which neeeds to interact with serialization sub-system to manage <see cref="IComplexProperty"/>
/// </summary>
/// <remarks>The implementation of <see cref="Convert(PreferredConversionType, ref object?)"/> and <see cref="ConvertBack(PreferredConversionType, ref object?)"/> shall be thread-safe and the class shall have at least a default initializer</remarks>
public interface IComplexTypeConverter : IComplexTypeConverterLogging
{
    /// <summary>
    /// The set of <see cref="Type"/> supported from the converter
    /// </summary>
    IEnumerable<Type> SupportedClrTypes { get; }
    /// <summary>
    /// The method is used from constructor of <see cref="IValueContainer{T}"/> to manage <see cref="IComplexProperty"/> that does not have an autonomous conversion
    /// </summary>
    /// <param name="conversionType">The preferred conversion can be applied, however the <see cref="IComplexTypeConverter"/> can decide autonomously</param>
    /// <param name="data">The input data coming from EF Core to be converted, the converted value shall return on the same reference</param>
    /// <returns><see langword="true"/> if the <see cref="IComplexTypeConverter"/> has converted the <paramref name="data"/>, if it not able to execute the conversion, or want to leave the management to the subsystem, shall return <see langword="false"/></returns>
    /// <remarks>The <see cref="MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage.DefaultValueContainer{TKey}"/> by default uses POCO serialization so it does not need to implement the method and can return <see langword="false"/>.</remarks>
    bool Convert(PreferredConversionType conversionType, ref object? data);
    /// <summary>
    /// The method is used from <see cref="IValueContainer{T}.GetData(IValueContainerMetadata, ref object[], IComplexTypeConverterFactory?)"/> and <see cref="IValueContainer{T}.GetProperties"/> to manage <see cref="IComplexProperty"/> that does not have an autonomous conversion
    /// </summary>
    /// <param name="conversionType">The preferred conversion can be applied, however the <see cref="IComplexTypeConverter"/> can decide autonomously</param>
    /// <param name="data">The input data coming from serialization to be converted back, the converted value shall return on the same reference</param>
    /// <returns><see langword="true"/> if the <see cref="IComplexTypeConverter"/> has converted the <paramref name="data"/>, if it not able to execute the conversion shall return <see langword="false"/></returns>
    /// <remarks>The <see cref="MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage.DefaultValueContainer{TKey}"/> by default uses POCO serialization so it does not need to implement the method and can return <see langword="false"/>.</remarks>
    bool ConvertBack(PreferredConversionType conversionType, ref object? data);
}
