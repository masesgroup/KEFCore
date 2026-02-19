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
            if (ManagedType == null || ManagedType == NativeTypeMapper.ManagedTypes.Undefined)
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
                        var convertingType = Type.GetType(ClrType!);
                        if (SupportNull && convertingType!.IsConstructedGenericType)
                        {
                            convertingType = convertingType.GenericTypeArguments[0];
                        }
                        Value = Convert.ChangeType(tmp, convertingType!);
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
    /// The <see cref="NativeTypeMapper.ManagedTypes"/> value of the <see cref="ClrType"/>
    /// </summary>
    public NativeTypeMapper.ManagedTypes? ManagedType { get; set; }
    /// <summary>
    /// <see langword="true"/> if <see cref="NativeTypeMapper.ManagedTypes"/> value of the <see cref="ClrType"/> supports <see langword="null"/>
    /// </summary>
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
    /// <param name="tName">The <see cref="IEntityType"/> requesting the <see cref="DefaultValueContainer{TKey}"/> for <paramref name="rData"/></param>
    /// <param name="properties">The set of <see cref="IProperty"/> deducted from <see cref="IEntityType.GetProperties"/>, if <see langword="null"/> the implmenting instance of <see cref="IValueContainer{T}"/> shall deduct it</param>
    /// <param name="rData">The data, built from EFCore, to be stored in the <see cref="DefaultValueContainer{TKey}"/></param>
    /// <remarks>This constructor is mandatory and it is used from KEFCore to request a <see cref="DefaultValueContainer{TKey}"/></remarks>
    public DefaultValueContainer(IEntityType tName, IProperty[]? properties, object[] rData)
    {
        properties ??= [.. tName.GetProperties()];
        EntityName = tName.Name;
        ClrType = tName.ClrType?.ToAssemblyQualified()!;
        Properties = [];
        foreach (var item in properties)
        {
            (NativeTypeMapper.ManagedTypes, bool) _type = NativeTypeMapper.GetValue(item.ClrType);
            var pRecord = new PropertyData
            {
                ManagedType = _type.Item1,
                SupportNull = _type.Item2,
                PropertyName = item.Name,
                ClrType = _type.Item1 == NativeTypeMapper.ManagedTypes.Undefined ? item.ClrType?.ToAssemblyQualified() : null,
                Value = rData[item.GetIndex()]
            };

            Properties.Add(pRecord);
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
    /// The data stored associated to the <see cref="IEntityType"/>
    /// </summary>
    public IList<PropertyData>? Properties { get; set; }
    /// <inheritdoc/>
    public void GetData(IEntityType tName, IProperty[]? properties, ref object[] array)
    {
        properties ??= [.. tName.GetProperties()];
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
            array = new object[properties.Length];
#if DEBUG_PERFORMANCE
            newSw.Stop();
            iterationSw.Start();
#endif
            if (Data != null)
            {
                foreach (var item in Data!)
                {
                    var prop = tName.FindProperty(item.Value?.PropertyName!);
                    if (prop == null) continue; // a property was removed from the schema 
                    array[prop.GetIndex()] = item.Value?.Value!;
                }
            }
            else
            {
                foreach (var item in Properties!)
                {
                    var prop = tName.FindProperty(item.PropertyName!);
                    if (prop == null) continue; // a property was removed from the schema 
                    array[prop.GetIndex()] = item?.Value!;
                }
            }
#if DEBUG_PERFORMANCE
            iterationSw.Stop();
            fullSw.Stop();
        }
        finally
        {
            if (Infrastructure.KafkaDbContext.TraceEntityTypeDataStorageGetData)
            {
                Infrastructure.KafkaDbContext.ReportString($"Time to GetData with length {Data?.Count}: {fullSw.Elapsed} - new array took: {newSw.Elapsed} - Iteration took: {iterationSw.Elapsed}");
            }
        }
#endif
    }
    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> GetProperties()
    {
        Dictionary<string, object> props = [];
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

        foreach (var item in Properties!)
        {
            props.Add(item.PropertyName!, item.Value!);
        }
        return props;
    }
}
