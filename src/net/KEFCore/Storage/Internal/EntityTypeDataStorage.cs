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

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
[JsonSerializable(typeof(ObjectType))]
public class ObjectType : IJsonOnDeserialized
{
    public ObjectType()
    {

    }

    public ObjectType(IProperty typeName, object value)
    {
        TypeName = typeName.ClrType?.FullName;
        PropertyName = typeName.Name;
        Value = value;
    }

    public void OnDeserialized()
    {
        if (Value is JsonElement elem)
        {
            switch (elem.ValueKind)
            {
                case JsonValueKind.String:
                    Value = elem.GetString()!;
                    if (TypeName != typeof(string).FullName)
                    {
                        try
                        {
                            Value = Convert.ChangeType(Value, Type.GetType(TypeName!)!);
                        }
                        catch (InvalidCastException)
                        {
                            // failed conversion, try with other methods for known types
                            if (TypeName == typeof(Guid).FullName)
                            {
                                Value = elem.GetGuid();
                            }
                            else if (TypeName == typeof(DateTime).FullName)
                            {
                                Value = elem.GetDateTime();
                            }
                            else if (TypeName == typeof(DateTimeOffset).FullName)
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
                    Value = Convert.ChangeType(tmp, Type.GetType(TypeName!)!);
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
            Value = Convert.ChangeType(Value, Type.GetType(TypeName!)!);
        }
    }

    public string? TypeName { get; set; }

    public string? PropertyName { get; set; }

    public object Value { get; set; }
}

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
[JsonSerializable(typeof(EntityTypeDataStorage<>))]
public class EntityTypeDataStorage<TKey> : IEntityTypeData
{
    public EntityTypeDataStorage() { }

    public EntityTypeDataStorage(IEntityType tName, IProperty[] properties, object[] rData)
    {
        TypeName = tName.Name;
        Data = new Dictionary<int, ObjectType>();
        for (int i = 0; i < properties.Length; i++)
        {
            Data.Add(properties[i].GetIndex(), new ObjectType(properties[i], rData[i]));
        }
    }

    public string TypeName { get; set; }

    public Dictionary<int, ObjectType> Data { get; set; }

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
            array[i] = Data[i].Value;
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
