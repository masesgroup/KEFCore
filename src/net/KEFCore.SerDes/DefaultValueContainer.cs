/*
*  Copyright 2022 - 2025 MASES s.r.l.
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
    /// <summary>
    /// Initialize a new instance of <see cref="PropertyData"/>
    /// </summary>
    /// <remarks>It is mainly used from the JSON serializer</remarks>
    public PropertyData()
    {

    }
    /// <summary>
    /// Initialize a new instance of <see cref="PropertyData"/>
    /// </summary>
    /// <param name="property">The <see cref="IProperty"/> to be stored into <see cref="PropertyData"/> associated to <paramref name="value"/></param>
    /// <param name="value">The data, built from EFCore, to be stored in the <see cref="PropertyData"/></param>
    /// <remarks>This constructor is mandatory and it is used from <see cref="DefaultValueContainer{TKey}"/></remarks>
    public PropertyData(IProperty property, object value)
    {
        (NativeTypeMapper.ManagedTypes, bool) _type = NativeTypeMapper.GetValue(property.ClrType);
        ManagedType = _type.Item1;
        SupportNull = _type.Item2;
        ClrType = property.ClrType?.ToAssemblyQualified();
        PropertyName = property.Name;
        Value = value;
    }
    /// <inheritdoc/>
    public void OnDeserialized()
    {
        if (Value is JsonElement elem)
        {
            if (ManagedType == null || ManagedType == NativeTypeMapper.ManagedTypes.Undefined)
            {
                switch (elem.ValueKind)
                {
                    case JsonValueKind.String:
                        Value = elem.GetString()!;
                        if (ClrType != typeof(string).ToAssemblyQualified())
                        {
                            try
                            {
                                Value = Convert.ChangeType(Value, Type.GetType(ClrType!)!);
                            }
                            catch (InvalidCastException)
                            {
                                // failed conversion, try with other methods for known types
                                if (ClrType == typeof(Guid).ToAssemblyQualified())
                                {
                                    Value = elem.GetGuid();
                                }
                                else if (ClrType == typeof(DateTime).ToAssemblyQualified())
                                {
                                    Value = elem.GetDateTime();
                                }
                                else if (ClrType == typeof(DateTimeOffset).ToAssemblyQualified())
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
                        var convertingType = Type.GetType(ClrType!);
                        if (SupportNull && convertingType!.IsConstructedGenericType)
                        {
                            convertingType = convertingType.GenericTypeArguments[0];
                        }
                        else
                        {
                            Value = Convert.ChangeType(tmp, convertingType!);
                        }
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
                        throw new InvalidOperationException($"Failed to deserialize {PropertyName}, ValueKind is {elem.ValueKind}");
                }
            }
            else
            {
                switch (elem.ValueKind)
                {
                    case JsonValueKind.String:
                        Value = elem.GetString()!;
                        if (ManagedType != NativeTypeMapper.ManagedTypes.String)
                        {
                            switch (ManagedType.Value)
                            {
                                case NativeTypeMapper.ManagedTypes.Guid:
                                    Value = elem.GetGuid();
                                    break;
                                case NativeTypeMapper.ManagedTypes.DateTime:
                                    Value = elem.GetDateTime();
                                    break;
                                case NativeTypeMapper.ManagedTypes.DateTimeOffset:
                                    Value = elem.GetDateTimeOffset();
                                    break;
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
                    case JsonValueKind.Array:
                    case JsonValueKind.Undefined:
                    default:
                        throw new InvalidOperationException($"Failed to deserialize {PropertyName}, ValueKind is {elem.ValueKind}");
                }
            }
        }
        else
        {
            Value = Value != null ? Convert.ChangeType(Value, Type.GetType(ClrType!)!) : Value;
        }
    }
    /// <summary>
    /// The name of the <see cref="IProperty"/>
    /// </summary>
    public string? PropertyName { get; set; }
    /// <summary>
    /// The <see cref="NativeTypeMapper.ManagedTypes"/> value of the <see cref="ClrType"/>
    /// </summary>
    public NativeTypeMapper.ManagedTypes? ManagedType { get; set; }
    /// <summary>
    /// <see langword="true"/> if <see cref="NativeTypeMapper.ManagedTypes"/> value of the <see cref="ClrType"/> supports <see langword="null"/>
    /// </summary>
    public bool SupportNull { get; set; }
    /// <summary>
    /// The full name of the CLR <see cref="Type"/> of the <see cref="IProperty"/>
    /// </summary>
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
    /// <param name="tName">The <see cref="IEntityType"/> requesting the <see cref="DefaultValueContainer{TKey}"/> for <paramref name="rData"/></param>
    /// <param name="rData">The data, built from EFCore, to be stored in the <see cref="DefaultValueContainer{TKey}"/></param>
    /// <remarks>This constructor is mandatory and it is used from KEFCore to request a <see cref="DefaultValueContainer{TKey}"/></remarks>
    public DefaultValueContainer(IEntityType tName, object[] rData)
    {
        EntityName = tName.Name;
        ClrType = tName.ClrType?.ToAssemblyQualified()!;
        Data = new Dictionary<int, PropertyData>();
        foreach (var item in tName.GetProperties())
        {
            int index = item.GetIndex();
            Data.Add(index, new PropertyData(item, rData[index]));
        }
    }
    /// <inheritdoc/>
    public string EntityName { get; set; }
    /// <inheritdoc/>
    public string ClrType { get; set; }
    /// <summary>
    /// The data stored associated to the <see cref="IEntityType"/>
    /// </summary>
    public Dictionary<int, PropertyData>? Data { get; set; }
    /// <inheritdoc/>
    public void GetData(IEntityType tName, ref object[] array)
    {
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
            array = new object[Data.Count];
#if DEBUG_PERFORMANCE
            newSw.Stop();
            iterationSw.Start();
#endif
            for (int i = 0; i < Data.Count; i++)
            {
                array[i] = Data[i].Value!;
            }
#if DEBUG_PERFORMANCE
            iterationSw.Stop();
            fullSw.Stop();
        }
        finally
        {
            if (Infrastructure.KafkaDbContext.TraceEntityTypeDataStorageGetData)
            {
                Infrastructure.KafkaDbContext.ReportString($"Time to GetData with length {Data.Count}: {fullSw.Elapsed} - new array took: {newSw.Elapsed} - Iteration took: {iterationSw.Elapsed}");
            }
        }
#endif
    }
    /// <inheritdoc/>
    public IReadOnlyDictionary<int, string> GetProperties()
    {
        Dictionary<int, string> props = new();
        for (int i = 0; i < Data!.Count; i++)
        {
            props.Add(i, Data[i].PropertyName!);
        }
        return props;
    }
}
