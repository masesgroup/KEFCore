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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage;
/// <summary>
/// This is a supporting class used from <see cref="DefaultValueContainer{TKey}"/>
/// </summary>
public class PropertyData : IJsonOnDeserialized
{
    static readonly string StringAssemblyQualified = typeof(string).ToAssemblyQualified();
    static readonly string GuidAssemblyQualified = typeof(Guid).ToAssemblyQualified();
    static readonly string DateTimeAssemblyQualified = typeof(DateTime).ToAssemblyQualified();
    static readonly string DateTimeOffsetAssemblyQualified = typeof(DateTimeOffset).ToAssemblyQualified();

    /// <inheritdoc/>
    public void OnDeserialized()
    {
        if (Value is JsonElement elem)
        {
            if (ManagedType == null || ManagedType == WellKnownManagedTypes.Undefined)
            {
                switch (elem.ValueKind)
                {
                    case JsonValueKind.String:
                        Value = elem.GetString()!;
                        if (!string.IsNullOrWhiteSpace(ClrType) && ClrType != StringAssemblyQualified)
                        {
                            try
                            {
                                Value = Convert.ChangeType(Value, Type.GetType(ClrType!)!);
                            }
                            catch (InvalidCastException)
                            {
                                // failed conversion, try with other methods for known types
                                if (ClrType == GuidAssemblyQualified)
                                {
                                    Value = elem.GetGuid();
                                }
                                else if (ClrType == DateTimeAssemblyQualified)
                                {
                                    Value = elem.GetDateTime();
                                }
                                else if (ClrType == DateTimeOffsetAssemblyQualified)
                                {
                                    Value = elem.GetDateTimeOffset();
                                }
                                else
                                {
                                    Value = elem.GetString()!;
                                }
                            }
                        }
                        break;
                    case JsonValueKind.Number:
                        var tmp = elem.GetInt64();
                        if (ClrType != null)
                        {
                            var convertingType = Type.GetType(ClrType!);
                            if (SupportNull)
                            {
                                Type? type = Nullable.GetUnderlyingType(convertingType!);
                                convertingType = type ?? convertingType;
                            }
                            Value = Convert.ChangeType(tmp, convertingType!);
                        }
                        else tmp.ToString();
                        break;
                    case JsonValueKind.True:
                        Value = true;
                        break;
                    case JsonValueKind.False:
                        Value = false;
                        break;
                    case JsonValueKind.Null:
                        Value = null;
                        break;
                    case JsonValueKind.Object:
                    case JsonValueKind.Array:
                    case JsonValueKind.Undefined:
                    default:
                        throw new InvalidOperationException($"Failed to deserialize {Value} as {ClrType}, ValueKind is {elem.ValueKind}");
                }
            }
            else
            {
                switch (elem.ValueKind)
                {
                    case JsonValueKind.String:
                        Value = elem.GetString()!;
                        if (ManagedType != WellKnownManagedTypes.String)
                        {
                            switch (ManagedType.Value)
                            {
                                case WellKnownManagedTypes.Guid:
                                    Value = elem.GetGuid();
                                    break;
                                case WellKnownManagedTypes.DateTime:
                                    Value = elem.GetDateTime();
                                    break;
                                case WellKnownManagedTypes.DateTimeOffset:
                                    Value = elem.GetDateTimeOffset();
                                    break;
                                case WellKnownManagedTypes.ComplexType: break;
                                default:
                                    try
                                    {
                                        Value = Convert.ChangeType(Value, NativeTypeMapper.GetValue((ManagedType.Value, SupportNull)));
                                    }
                                    catch (InvalidCastException)
                                    {
                                        Value = elem.GetString()!;
                                    }
                                    break;
                            }
                        }
                        break;
                    case JsonValueKind.Number:
                        var tmp = elem.GetInt64();
                        Value = Convert.ChangeType(tmp, NativeTypeMapper.GetValue((ManagedType.Value, false)));
                        break;
                    case JsonValueKind.True:
                        Value = true;
                        break;
                    case JsonValueKind.False:
                        Value = false;
                        break;
                    case JsonValueKind.Null:
                        Value = null;
                        break;
                    case JsonValueKind.Object:
                        if (ManagedType == WellKnownManagedTypes.ComplexType)
                        {
                            break;
                        }
                        else throw new InvalidOperationException($"Failed to deserialize {Value} as {ClrType}, ValueKind is {elem.ValueKind}");
                    case JsonValueKind.Array:
                    case JsonValueKind.Undefined:
                    default:
                        throw new InvalidOperationException($"Failed to deserialize {Value} as {ClrType}, ValueKind is {elem.ValueKind}");
                }
            }
        }
        else
        {
            Type type = NativeTypeMapper.GetValue((ManagedType!.Value, SupportNull));
            if (type == null && !string.IsNullOrWhiteSpace(ClrType)) type = Type.GetType(ClrType!)!;
            Value = Value != null ? Convert.ChangeType(Value, type!) : Value;
        }
    }
    /// <summary>
    /// The value associated to the <see cref="IReadOnlyPropertyBase.Name"/>
    /// </summary>
    public string? PropertyName { get; set; }
    /// <summary>
    /// The <see cref="WellKnownManagedTypes"/> value of the <see cref="ClrType"/>
    /// </summary>
    public WellKnownManagedTypes? ManagedType { get; set; }
    /// <summary>
    /// <see langword="true"/> if <see cref="WellKnownManagedTypes"/> value of the <see cref="ClrType"/> supports <see langword="null"/>
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SupportNull { get; set; }
    /// <summary>
    /// The full name of the CLR <see cref="Type"/> of the <see cref="IProperty"/>, null for well-known types
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClrType { get; set; }
    /// <summary>
    /// The raw value associated to the <see cref="IProperty"/>
    /// </summary>
    public object? Value { get; set; }
}
/// <summary>
/// The default ValueContainer used from KEFCore
/// </summary>
/// <typeparam name="TKey">It is the key <see cref="Type"/> passed from Entity Framework associated to the Entity data will be stored in the <see cref="DefaultValueContainer{TKey}"/></typeparam>
public class DefaultValueContainer<TKey> : IValueContainer<TKey> where TKey : notnull
{
    /// <summary>
    /// Initialize a new instance of <see cref="DefaultValueContainer{TKey}"/>
    /// </summary>
    /// <remarks>It is mainly used from the JSON serializer</remarks>
    public DefaultValueContainer() { EntityName = ClrType = null!; }
    /// <summary>
    /// Initialize a new instance of <see cref="DefaultValueContainer{TKey}"/>
    /// </summary>
    /// <param name="valueContainerData">The <see cref="IValueContainerData"/> containing the information to prepare an instance of <see cref="DefaultValueContainer{TKey}"/></param>
    /// <param name="complexTypeFactory">The instance of <see cref="IComplexTypeConverterFactory"/> will manage strong type conversion</param>
    /// <remarks>This constructor is mandatory and it is used from KEFCore to request a <see cref="DefaultValueContainer{TKey}"/></remarks>
    public DefaultValueContainer(IValueContainerData valueContainerData, IComplexTypeConverterFactory? complexTypeFactory = null)
    {
        var tName = valueContainerData.EntityType;
        var properties = valueContainerData.Properties;
        var propertyValues = valueContainerData.PropertyValues;
        var complexProperties = valueContainerData.ComplexProperties;
        var complexPropertyValues = valueContainerData.ComplexPropertyValues;

        properties ??= [.. tName.GetProperties()];
        EntityName = tName.Name;
        ClrType = tName.ClrType?.ToAssemblyQualified()!;
        Properties = new PropertyData[properties.Length + (complexProperties != null && complexPropertyValues != null ? complexProperties.Length : 0)];
        for (int i = 0; i < properties.Length; i++)
        {
            (WellKnownManagedTypes, bool) _type = NativeTypeMapper.GetValue(properties[i].ClrType);
            Properties[i] = new PropertyData
            {
                ManagedType = _type.Item1,
                SupportNull = _type.Item2,
                PropertyName = properties[i].Name,
                ClrType = _type.Item1 == WellKnownManagedTypes.Undefined ? properties[i].ClrType?.ToAssemblyQualified() : null,
                Value = propertyValues[i]
            };
        }
        if (complexProperties != null && complexPropertyValues != null)
        {
            for (int i = 0; i < complexProperties.Length; i++)
            {
                var item = complexProperties[i];
                var type = WellKnownManagedTypes.ComplexType;
                IComplexTypeConverter? complexTypeHook = null;
                complexTypeFactory?.TryGet(complexProperties[i], out complexTypeHook);
                if (complexTypeHook == null || !complexTypeHook.Convert(PreferredConversionType.Text, ref complexPropertyValues[i]!))
                {
                    complexPropertyValues[i] = JsonSupport.ValueContainer.Serialize(item.ClrType, complexPropertyValues[i]!);
                    type = WellKnownManagedTypes.ComplexTypeAsJson;
                }

                Properties[i + properties.Length] = new PropertyData
                {
                    ManagedType = type,
                    SupportNull = false,
                    PropertyName = complexProperties[i].Name,
                    ClrType = complexProperties[i].ClrType?.ToAssemblyQualified(),
                    Value = complexPropertyValues[i]
                };
            }
        }
    }
    /// <inheritdoc/>
    public string EntityName { get; set; }
    /// <inheritdoc/>
    public string ClrType { get; set; }
    /// <summary>
    /// The data stored associated to the <see cref="IEntityType"/>
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, PropertyData>? Data { get; set; }
    /// <summary>
    /// The data stored associated to the <see cref="IProperty"/> and <see cref="IComplexProperty"/> of <see cref="IEntityType"/>
    /// </summary>
    public PropertyData[]? Properties { get; set; }
    /// <inheritdoc/>
    public void GetData(IValueContainerMetadata metadata, ref object[] allPropertyValues, IComplexTypeConverterFactory? complexTypeFactory = null)
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
            if (Data == null && Properties == null) { return; }
#if DEBUG_PERFORMANCE
            newSw.Start();
#endif

            allPropertyValues = new object[flattenedProperties!.Length];
#if DEBUG_PERFORMANCE
            newSw.Stop();
            iterationSw.Start();
#endif
            if (Data != null)
            {
                foreach (var item in Data!)
                {
                    var prop = metadata.EntityType.FindProperty(item.Value?.PropertyName!);
                    if (prop == null) continue; // a property was removed from the schema 
                    allPropertyValues[item.Key] = item.Value?.Value!;
                }
            }
            else
            {
                if (complexProperties == null || complexProperties.Length == 0) // avoid complex flux without complex properties
                {
                    for (int i = 0; i < Properties.Length; i++)
                    {
                        if (NativeTypeMapper.IsComplex(Properties[i].ManagedType)) continue;

                        IPropertyBase? prop = tName.FindProperty(Properties[i].PropertyName!);
                        if (prop == null) continue; // a property was removed from the schema
                        allPropertyValues[i] = Properties[i]?.Value!;
                    }
                }
                else
                {
                    Dictionary<IPropertyBase, object> propertiesInfo = new();
                    Dictionary<IComplexProperty, object> complexPropertiesInfo = new();
                    for (int i = 0; i < Properties.Length; i++)
                    {
                        IPropertyBase? prop = NativeTypeMapper.IsComplex(Properties[i].ManagedType)
                            ? tName.FindComplexProperty(Properties[i].PropertyName!)
                            : tName.FindProperty(Properties[i].PropertyName!);
                        if (prop == null) continue; // a property was removed from the schema
                        if (prop is IComplexProperty complexProperty)
                        {
                            var input = Properties[i]?.Value!;
                            if (Properties[i].ManagedType == WellKnownManagedTypes.ComplexTypeAsJson && input is string str)
                            {
                                input = JsonSupport.ValueContainer.Deserialize(prop.ClrType, str);
                            }
                            else if (Properties[i]?.ManagedType == WellKnownManagedTypes.ComplexType &&
                                complexTypeFactory != null && complexTypeFactory.TryGet(prop, out var complexTypeHook))
                            {
                                complexTypeHook?.ConvertBack(PreferredConversionType.Text, ref input);
                            }
                            else throw new InvalidCastException($"Cannot manage record value {allPropertyValues[i]}.");
                            complexPropertiesInfo.Add(complexProperty, input!);
                        }
                        else
                        {
                            propertiesInfo.Add(prop, Properties[i]?.Value!);
                        }
                    }

                    flattenedProperties.FillFlattened(tName, propertiesInfo, complexPropertiesInfo, ref allPropertyValues);
                }
            }
#if DEBUG_PERFORMANCE
            iterationSw.Stop();
            fullSw.Stop();
        }
        finally
        {
            if (Internal.DebugPerformanceHelper.TraceEntityTypeDataStorageGetData)
            {
                Internal.DebugPerformanceHelper.ReportString($"Time to GetData with length {Data?.Count}: {fullSw.Elapsed} - new array took: {newSw.Elapsed} - Iteration took: {iterationSw.Elapsed}");
            }
        }
#endif
    }
    /// <inheritdoc/>
    public IDictionary<string, object?> GetProperties(IEntityType? entityType)
    {
        Dictionary<string, object?> props = [];
        if (Data == null && Properties == null) { return props; }

        if (Data != null)
        {
            // old implementation
            foreach (var item in Data!)
            {
                props.Add(item.Value.PropertyName!, item.Value.Value!);
            }
            return props;
        }
        object? value;
        foreach (var item in Properties!)
        {
            value = item.Value!;
            if (NativeTypeMapper.IsComplex(item.ManagedType)) continue;
            props.Add(item.PropertyName!, value);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(props);
    }

    /// <inheritdoc/>
    public IDictionary<string, object?> GetComplexProperties(IEntityType? entityType, IComplexTypeConverterFactory? complexTypeFactory)
    {
        Dictionary<string, object?> props = [];
        if (Data == null && Properties == null) { return props; }

        object? value;
        foreach (var item in Properties!)
        {
            if (!NativeTypeMapper.IsComplex(item.ManagedType)) continue;
            value = item.Value!;
            Type propertyType = entityType?.FindComplexProperty(item.PropertyName!)?.ClrType! ?? Type.GetType(item.ClrType!)!;
            if (item.ManagedType == WellKnownManagedTypes.ComplexTypeAsJson && value is string str)
            {
                value = JsonSupport.ValueContainer.Deserialize(propertyType, str);
            }
            else if (complexTypeFactory != null && complexTypeFactory.TryGet(propertyType, out IComplexTypeConverter? complexTypeHook))
            {
                complexTypeHook?.ConvertBack(PreferredConversionType.Text, ref value!);
            }
            else throw new InvalidCastException($"Cannot manage record value {value}.");
            props.Add(item.PropertyName!, value);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(props);
    }
}
