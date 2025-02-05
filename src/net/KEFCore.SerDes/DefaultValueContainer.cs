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

// #define DEBUG_PERFORMANCE

#nullable enable

using MASES.KNet.Serialization;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Json.Storage;
/// <summary>
/// This is a supporting class used from <see cref="DefaultValueContainer{TKey}"/>
/// </summary>
public class PropertyData : IJsonOnDeserialized
{
    static readonly ConcurrentDictionary<Type, ManagedTypes> dict = new();
    static readonly ConcurrentDictionary<ManagedTypes, Type> reverseDict = new();
    readonly static Type StringType = typeof(string);
    readonly static Type GuidType = typeof(Guid);
    readonly static Type DateTimeType = typeof(DateTime);
    readonly static Type DateTimeOffsetType = typeof(DateTimeOffset);
    readonly static Type ByteType = typeof(byte);
    readonly static Type ShortType = typeof(short);
    readonly static Type IntType = typeof(int);
    readonly static Type LongType = typeof(long);
    readonly static Type DoubleType = typeof(double);
    readonly static Type FloatType = typeof(float);

    /// <summary>
    /// List of <see cref="Type"/> managed from <see cref="PropertyData"/>
    /// </summary>
    public enum ManagedTypes
    {
        /// <summary>
        /// Not defined or not found
        /// </summary>
        Undefined,
        /// <summary>
        /// <see cref="string"/>
        /// </summary>
        String,
        /// <summary>
        /// <see cref="Guid"/>
        /// </summary>
        Guid,
        /// <summary>
        /// <see cref="DateTime"/>
        /// </summary>
        DateTime,
        /// <summary>
        /// <see cref="DateTimeOffset"/>
        /// </summary>
        DateTimeOffset,
        /// <summary>
        /// <see cref="byte"/>
        /// </summary>
        Byte,
        /// <summary>
        /// <see cref="short"/>
        /// </summary>
        Short,
        /// <summary>
        /// <see cref="int"/>
        /// </summary>
        Int,
        /// <summary>
        /// <see cref="long"/>
        /// </summary>
        Long,
        /// <summary>
        /// <see cref="double"/>
        /// </summary>
        Double,
        /// <summary>
        /// <see cref="float"/>
        /// </summary>
        Float
    }

    static PropertyData()
    {
        dict.TryAdd(StringType, ManagedTypes.String);reverseDict.TryAdd(ManagedTypes.String, StringType);
        dict.TryAdd(GuidType, ManagedTypes.Guid); reverseDict.TryAdd(ManagedTypes.Guid, GuidType);
        dict.TryAdd(DateTimeType, ManagedTypes.DateTime); reverseDict.TryAdd(ManagedTypes.DateTime, DateTimeType);
        dict.TryAdd(DateTimeOffsetType, ManagedTypes.DateTimeOffset); reverseDict.TryAdd(ManagedTypes.DateTimeOffset, DateTimeOffsetType);
        dict.TryAdd(ByteType, ManagedTypes.Byte); reverseDict.TryAdd(ManagedTypes.Byte, ByteType);
        dict.TryAdd(ShortType, ManagedTypes.Short); reverseDict.TryAdd(ManagedTypes.Short, ShortType);
        dict.TryAdd(IntType, ManagedTypes.Int); reverseDict.TryAdd(ManagedTypes.Int, IntType);
        dict.TryAdd(LongType, ManagedTypes.Long); reverseDict.TryAdd(ManagedTypes.Long, LongType);
        dict.TryAdd(DoubleType, ManagedTypes.Double); reverseDict.TryAdd(ManagedTypes.Double, DoubleType);
        dict.TryAdd(FloatType, ManagedTypes.Float); reverseDict.TryAdd(ManagedTypes.Float, FloatType);
    }

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
        if (!dict.TryGetValue(property.ClrType, out ManagedTypes _type)) _type = ManagedTypes.Undefined;
        ManagedType = _type;
        ClrType = property.ClrType?.ToAssemblyQualified();
        PropertyName = property.Name;
        Value = value;
    }
    /// <inheritdoc/>
    public void OnDeserialized()
    {
        if (Value is JsonElement elem)
        {
            if (ManagedType == null || ManagedType == ManagedTypes.Undefined)
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
                switch (elem.ValueKind)
                {
                    case JsonValueKind.String:
                        Value = elem.GetString()!;
                        if (ManagedType != ManagedTypes.String)
                        {
                            try
                            {
                                
                                Value = Convert.ChangeType(Value, reverseDict[ManagedType.Value]);
                            }
                            catch (InvalidCastException)
                            {
                                // failed conversion, try with other methods for known types
                                if (ManagedType == ManagedTypes.Guid)
                                {
                                    Value = elem.GetGuid();
                                }
                                else if (ManagedType == ManagedTypes.DateTime)
                                {
                                    Value = elem.GetDateTime();
                                }
                                else if (ManagedType == ManagedTypes.DateTimeOffset)
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
                        Value = Convert.ChangeType(tmp, reverseDict[ManagedType.Value]);
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
            Value = Convert.ChangeType(Value, Type.GetType(ClrType!)!);
        }
    }
    /// <summary>
    /// The name of the <see cref="IProperty"/>
    /// </summary>
    public string? PropertyName { get; set; }
    /// <summary>
    /// The <see cref="ManagedTypes"/> value of the <see cref="ClrType"/>
    /// </summary>
    public ManagedTypes? ManagedType { get; set; }
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
