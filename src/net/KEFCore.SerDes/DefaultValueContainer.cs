/*
*  Copyright 2023 MASES s.r.l.
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

// #define DEBUG_PERFORMANCE

#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Storage;
/// <summary>
/// This is a supporting class used from <see cref="DefaultValueContainer{TKey}"/>
/// </summary>
[JsonSerializable(typeof(PropertyData))]
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
        ClrType = property.ClrType?.FullName;
        PropertyName = property.Name;
        Value = value;
    }
    /// <inheritdoc/>
    public void OnDeserialized()
    {
        if (Value is JsonElement elem)
        {
            switch (elem.ValueKind)
            {
                case JsonValueKind.String:
                    Value = elem.GetString()!;
                    if (ClrType != typeof(string).FullName)
                    {
                        try
                        {
                            Value = Convert.ChangeType(Value, Type.GetType(ClrType!)!);
                        }
                        catch (InvalidCastException)
                        {
                            // failed conversion, try with other methods for known types
                            if (ClrType == typeof(Guid).FullName)
                            {
                                Value = elem.GetGuid();
                            }
                            else if (ClrType == typeof(DateTime).FullName)
                            {
                                Value = elem.GetDateTime();
                            }
                            else if (ClrType == typeof(DateTimeOffset).FullName)
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
                    Value = Convert.ChangeType(tmp, Type.GetType(ClrType!)!);
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
            Value = Convert.ChangeType(Value, Type.GetType(ClrType!)!);
        }
    }
    /// <summary>
    /// The name of the <see cref="IProperty"/>
    /// </summary>
    public string? PropertyName { get; set; }
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
[JsonSerializable(typeof(DefaultValueContainer<>))]
public class DefaultValueContainer<TKey> : IValueContainer<TKey> where TKey : notnull
{
    /// <summary>
    /// Initialize a new instance of <see cref="DefaultValueContainer{TKey}"/>
    /// </summary>
    /// <remarks>It is mainly used from the JSON serializer</remarks>
    public DefaultValueContainer() { }
    /// <summary>
    /// Initialize a new instance of <see cref="DefaultValueContainer{TKey}"/>
    /// </summary>
    /// <param name="tName">The <see cref="IEntityType"/> requesting the <see cref="DefaultValueContainer{TKey}"/> for <paramref name="rData"/></param>
    /// <param name="rData">The data, built from EFCore, to be stored in the <see cref="DefaultValueContainer{TKey}"/></param>
    /// <remarks>This constructor is mandatory and it is used from KEFCore to request a <see cref="DefaultValueContainer{TKey}"/></remarks>
    public DefaultValueContainer(IEntityType tName, object[] rData)
    {
        EntityName = tName.Name;
        ClrType = tName.ClrType.FullName!;
        Data = new Dictionary<int, PropertyData>();
        foreach (var item in tName.GetProperties())
        {
            int index = item.GetIndex();
            Data.Add(index, new PropertyData(item, rData[index]));
        }
    }
    /// <summary>
    /// The CLR <see cref="Type"/> of <see cref="IEntityType"/>
    /// </summary>
    public string? EntityName { get; set; }
    /// <summary>
    /// The CLR <see cref="Type"/> of <see cref="IEntityType"/>
    /// </summary>
    public string? ClrType { get; set; }
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
}
