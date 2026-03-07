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

//#define DEBUG_PERFORMANCE

#nullable enable

using MASES.KNet.Serialization;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage;

/// <summary>
/// The default ValueContainer used from KEFCore
/// </summary>
/// <typeparam name="TKey">It is the key <see cref="Type"/> passed from Entity Framework associated to the Entity data will be stored in the <see cref="AvroValueContainer{TKey}"/></typeparam>
public partial class AvroValueContainer<TKey> : AvroValueContainer, IValueContainer<TKey> where TKey : notnull
{
    /// <summary>
    /// Initialize a new instance of <see cref="AvroValueContainer{TKey}"/>
    /// </summary>
    /// <remarks>It is mainly used from the JSON serializer</remarks>
    public AvroValueContainer() { EntityName = ClrType = null!; }
    /// <summary>
    /// Initialize a new instance of <see cref="AvroValueContainer{TKey}"/>
    /// </summary>
    /// <param name="valueContainerData">The <see cref="IValueContainerData"/> containing the information to prepare an instance of <see cref="AvroValueContainer{TKey}"/></param>
    /// <param name="complexTypeFactory">The instance of <see cref="IComplexTypeConverterFactory"/> will manage strong type conversion</param>
    /// <remarks>This constructor is mandatory and it is used from KEFCore to request a <see cref="AvroValueContainer{TKey}"/></remarks>
    public AvroValueContainer(IValueContainerData valueContainerData, IComplexTypeConverterFactory? complexTypeFactory = null)
    {
        var tName = valueContainerData.EntityType;
        var properties = valueContainerData.Properties;
        var propertyValues = valueContainerData.PropertyValues;
        var complexProperties = valueContainerData.ComplexProperties;
        var complexPropertyValues = valueContainerData.ComplexPropertyValues;

        properties ??= [.. tName.GetProperties()];
        EntityName = tName.Name;
        ClrType = tName.ClrType?.ToAssemblyQualified()!;
        Data = [];
        for (int i = 0; i < properties.Length; i++)
        {
            var item = properties[i];
            (NativeTypeMapper.ManagedTypes, bool) _type = NativeTypeMapper.GetValue(item.ClrType!);
            BuildPropertyValue(item, _type.Item1, _type.Item2, ref propertyValues[i]!);
            var pRecord = new PropertyDataRecord
            {
                ManagedType = (int)_type.Item1,
                SupportNull = _type.Item2,
                PropertyName = properties[i].Name,
                ClrType = _type.Item1 == NativeTypeMapper.ManagedTypes.Undefined ? item.ClrType?.ToAssemblyQualified() : null,
                Value = propertyValues[i]
            };
            Data.Add(pRecord);
        }
        if (complexProperties != null && complexPropertyValues != null)
        {
            for (int i = 0; i < complexProperties.Length; i++)
            {
                var item = complexProperties[i];
                BuildPropertyValue(item, NativeTypeMapper.ManagedTypes.ComplexType, false, ref complexPropertyValues[i]!, complexTypeFactory);
                var pRecord = new PropertyDataRecord
                {
                    ManagedType = (int)NativeTypeMapper.ManagedTypes.ComplexType,
                    SupportNull = false,
                    PropertyName = item.Name,
                    ClrType = item.ClrType?.ToAssemblyQualified(),
                    Value = complexPropertyValues[i]
                };
                Data.Add(pRecord);
            }
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void BuildPropertyValue(IPropertyBase? property, NativeTypeMapper.ManagedTypes managedType, bool supportNull, ref object? inputValue, IComplexTypeConverterFactory? complexTypeFactory = null)
    {
        switch (managedType)
        {
            case NativeTypeMapper.ManagedTypes.Guid:
                inputValue = supportNull ? (inputValue as Guid?)?.ToString()! : ((Guid)inputValue).ToString();
                break;
            case NativeTypeMapper.ManagedTypes.DateTime:
                inputValue = supportNull ? (inputValue as DateTime?)?.ToString("O")! : ((DateTime)inputValue).ToString("O");
                break;
            case NativeTypeMapper.ManagedTypes.DateTimeOffset:
                inputValue = supportNull ? (inputValue as DateTimeOffset?)?.ToString("O")! : ((DateTimeOffset)inputValue).ToString("O");
                break;
            case NativeTypeMapper.ManagedTypes.Char:
                inputValue = supportNull ? (inputValue as char?)?.ToString(CultureInfo.InvariantCulture) : ((char)inputValue).ToString(CultureInfo.InvariantCulture);
                break;
            case NativeTypeMapper.ManagedTypes.SByte:
                inputValue = supportNull ? (inputValue as sbyte?)?.ToString(CultureInfo.InvariantCulture) : ((sbyte)inputValue).ToString(CultureInfo.InvariantCulture);
                break;
            case NativeTypeMapper.ManagedTypes.UShort:
                inputValue = supportNull ? (inputValue as ushort?)?.ToString(CultureInfo.InvariantCulture) : ((ushort)inputValue).ToString(CultureInfo.InvariantCulture);
                break;
            case NativeTypeMapper.ManagedTypes.UInt:
                inputValue = supportNull ? (inputValue as uint?)?.ToString(CultureInfo.InvariantCulture) : ((uint)inputValue).ToString(CultureInfo.InvariantCulture);
                break;
            case NativeTypeMapper.ManagedTypes.ULong:
                inputValue = supportNull ? (inputValue as ulong?)?.ToString(CultureInfo.InvariantCulture) : ((ulong)inputValue).ToString(CultureInfo.InvariantCulture);
                break;
            case NativeTypeMapper.ManagedTypes.Decimal:
                inputValue = supportNull ? (inputValue as decimal?)?.ToString("G29", CultureInfo.InvariantCulture) : ((decimal)inputValue).ToString("G29", CultureInfo.InvariantCulture);
                break;
            case NativeTypeMapper.ManagedTypes.ComplexType:
                if (complexTypeFactory != null && complexTypeFactory.TryGet(property, out var complexTypeHook))
                {
                    complexTypeHook?.Convert(PreferredConversionType.Binary, ref inputValue);
                }
                break;
            default: break; // no op
        }
    }

    void ConvertInnerData(PropertyDataRecord record, ref object? input, IPropertyBase? property = null, IComplexTypeConverterFactory? complexTypeFactory = null)
    {
        switch ((NativeTypeMapper.ManagedTypes)record.ManagedType)
        {
            case NativeTypeMapper.ManagedTypes.Guid:
                {
                    if (record.Value != null)
                    {
                        if (!Guid.TryParse(record.Value as string, out Guid guid)) throw new InvalidCastException($"Unable to convert {record.Value} (property '{record.PropertyName}', managed type {(NativeTypeMapper.ManagedTypes)record.ManagedType}) into {nameof(Guid)}");
                        input = guid;
                    }
                    else if (!record.SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.Guid)}");
                }
                break;
            case NativeTypeMapper.ManagedTypes.DateTime:
                {
                    if (record.Value != null)
                    {
                        if (!DateTime.TryParse(record.Value as string, out DateTime dt)) throw new InvalidCastException($"Unable to convert {record.Value} (property '{record.PropertyName}', managed type {(NativeTypeMapper.ManagedTypes)record.ManagedType}) into {nameof(DateTime)}");
                        input = dt;
                    }
                    else if (!record.SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.DateTime)}");
                }
                break;
            case NativeTypeMapper.ManagedTypes.DateTimeOffset:
                {
                    if (record.Value != null)
                    {
                        if (!DateTimeOffset.TryParse(record.Value as string, out DateTimeOffset dto)) throw new InvalidCastException($"Unable to convert {record.Value} (property '{record.PropertyName}', managed type {(NativeTypeMapper.ManagedTypes)record.ManagedType}) into {nameof(DateTimeOffset)}");
                        input = dto;
                    }
                    else if (!record.SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.DateTimeOffset)}");
                }
                break;
            case NativeTypeMapper.ManagedTypes.Char:
                {
                    if (record.Value != null)
                    {
                        if (!char.TryParse(record.Value as string, out char dec)) throw new InvalidCastException($"Unable to convert {record.Value} (property '{record.PropertyName}', managed type {(NativeTypeMapper.ManagedTypes)record.ManagedType}) into {nameof(System.Char)}");
                        input = dec;
                    }
                    else if (!record.SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.Char)}");
                }
                break;
            case NativeTypeMapper.ManagedTypes.SByte:
                {
                    if (record.Value != null)
                    {
                        if (!sbyte.TryParse(record.Value as string, out sbyte dec)) throw new InvalidCastException($"Unable to convert {record.Value} (property '{record.PropertyName}', managed type {(NativeTypeMapper.ManagedTypes)record.ManagedType}) into {nameof(System.SByte)}");
                        input = dec;
                    }
                    else if (!record.SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.SByte)}");
                }
                break;
            case NativeTypeMapper.ManagedTypes.UShort:
                {
                    if (record.Value != null)
                    {
                        if (!ushort.TryParse(record.Value as string, out ushort dec)) throw new InvalidCastException($"Unable to convert {record.Value} (property '{record.PropertyName}', managed type {(NativeTypeMapper.ManagedTypes)record.ManagedType}) into {nameof(System.UInt16)}");
                        input = dec;
                    }
                    else if (!record.SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.UInt16)}");
                }
                break;
            case NativeTypeMapper.ManagedTypes.UInt:
                {
                    if (record.Value != null)
                    {
                        if (!uint.TryParse(record.Value as string, out uint dec)) throw new InvalidCastException($"Unable to convert {record.Value} (property '{record.PropertyName}', managed type {(NativeTypeMapper.ManagedTypes)record.ManagedType}) into {nameof(System.UInt32)}");
                        input = dec;
                    }
                    else if (!record.SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.UInt32)}");
                }
                break;
            case NativeTypeMapper.ManagedTypes.ULong:
                {
                    if (record.Value != null)
                    {
                        if (!ulong.TryParse(record.Value as string, out ulong dec)) throw new InvalidCastException($"Unable to convert {record.Value} (property '{record.PropertyName}', managed type {(NativeTypeMapper.ManagedTypes)record.ManagedType}) into {nameof(System.UInt64)}");
                        input = dec;
                    }
                    else if (!record.SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.UInt64)}");
                }
                break;
            case NativeTypeMapper.ManagedTypes.Decimal:
                {
                    if (record.Value != null)
                    {
                        if (!decimal.TryParse(record.Value as string, out decimal dec)) throw new InvalidCastException($"Unable to convert {record.Value} (property '{record.PropertyName}', managed type {(NativeTypeMapper.ManagedTypes)record.ManagedType}) into {nameof(System.Decimal)}");
                        input = dec;
                    }
                    else if (!record.SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.Decimal)}");
                }
                break;
            case NativeTypeMapper.ManagedTypes.ComplexType:
                {
                    object value = record.Value;
                    if (complexTypeFactory != null)
                    {
                        if (property != null && complexTypeFactory.TryGet(property, out var complexTypeHook))
                        {
                            complexTypeHook?.ConvertBack(PreferredConversionType.Binary, ref value!);
                        }
                        else if (complexTypeFactory.TryGet(record.ClrType, out complexTypeHook))
                        {
                            complexTypeHook?.ConvertBack(PreferredConversionType.Binary, ref value!);
                        }
                    }
                    input = value;
                }
                break;
            default: input = record?.Value!; break;
        }
    }

    /// <inheritdoc/>
    public void GetData(IValueContainerMetadata metadata, ref object[] allPropertyValues, IComplexTypeConverterFactory? complexTypeFactory)
    {
        var tName = metadata.EntityType;
        var properties = metadata.Properties;
        properties ??= [.. tName.GetProperties()];
        var flattenedProperties = metadata.FlattenedProperties;
        flattenedProperties ??= [.. tName.GetFlattenedProperties()];
        var complexProperties = metadata.ComplexProperties;
#if DEBUG_PERFORMANCE
        Stopwatch fullSw = new Stopwatch();
        Stopwatch newSw = new Stopwatch();
        Stopwatch iterationSw = new Stopwatch();
        try
        {
            fullSw.Start();
#endif
            if (Data == null) { return; }
#if DEBUG_PERFORMANCE
            newSw.Start();
#endif
            allPropertyValues = new object[flattenedProperties!.Length];
#if DEBUG_PERFORMANCE
            newSw.Stop();
            iterationSw.Start();
#endif
            if (complexProperties == null || complexProperties.Length == 0) // avoid complex flux without complex properties
            {
                for (int i = 0; i < Data.Count; i++)
                {
                    IPropertyBase? prop = Data[i].ManagedType == (int)NativeTypeMapper.ManagedTypes.ComplexType
                        ? tName.FindComplexProperty(Data[i].PropertyName!)
                        : tName.FindProperty(Data[i].PropertyName!);
                    if (prop == null) continue; // a property was removed from the schema 
                    ConvertInnerData(Data[i], ref allPropertyValues[i]!, prop, complexTypeFactory);
                }
            }
            else
            {
                Dictionary<IPropertyBase, object> propertiesInfo = new();
                Dictionary<IComplexProperty, object> complexPropertiesInfo = new();
                for (int i = 0; i < Data.Count; i++)
                {
                    IPropertyBase? prop = Data[i].ManagedType == (int)NativeTypeMapper.ManagedTypes.ComplexType
                         ? tName.FindComplexProperty(Data[i].PropertyName!)
                         : tName.FindProperty(Data[i].PropertyName!);
                    if (prop == null) continue; // a property was removed from the schema
                    object input = null!;
                    ConvertInnerData(Data[i], ref input!, prop, complexTypeFactory);
                    if (prop is IComplexProperty complexProperty)
                    {
                        complexPropertiesInfo.Add(complexProperty, input);
                    }
                    else
                    {
                        propertiesInfo.Add(prop, input);
                    }
                }

                flattenedProperties.FillFlattened(propertiesInfo, complexPropertiesInfo, ref allPropertyValues);
            }
#if DEBUG_PERFORMANCE
            iterationSw.Stop();
            fullSw.Stop();
        }
        finally
        {
            if (Internal.DebugPerformanceHelper.TraceEntityTypeDataStorageGetData)
            {
                Internal.DebugPerformanceHelper.ReportString($"Time to GetData with length {Data.Count}: {fullSw.Elapsed} - new array took: {newSw.Elapsed} - Iteration took: {iterationSw.Elapsed}");
            }
        }
#endif
    }
    /// <inheritdoc/>
    public IDictionary<string, object?> GetProperties(IComplexTypeConverterFactory? complexTypeFactory)
    {
        Dictionary<string, object?> props = new();
        object? result = null!;
        foreach (var item in Data)
        {
            if (item.ManagedType == (int)NativeTypeMapper.ManagedTypes.ComplexType) continue;
            ConvertInnerData(item, ref result, complexTypeFactory: complexTypeFactory);
            props.Add(item.PropertyName, result);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(props);
    }

    public IDictionary<string, object?> GetComplexProperties(IComplexTypeConverterFactory? complexTypeFactory)
    {
        Dictionary<string, object?> props = new();
        object? result = null!;
        foreach (var item in Data)
        {
            if (item.ManagedType != (int)NativeTypeMapper.ManagedTypes.ComplexType) continue;
            ConvertInnerData(item, ref result, complexTypeFactory: complexTypeFactory);
            props.Add(item.PropertyName, result);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(props);
    }
}
