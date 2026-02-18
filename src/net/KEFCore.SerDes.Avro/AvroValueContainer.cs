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
    /// <param name="tName">The <see cref="IEntityType"/> requesting the <see cref="AvroValueContainer{TKey}"/> for <paramref name="rData"/></param>
    /// <param name="rData">The data, built from EFCore, to be stored in the <see cref="AvroValueContainer{TKey}"/></param>
    /// <remarks>This constructor is mandatory and it is used from KEFCore to request a <see cref="AvroValueContainer{TKey}"/></remarks>
    public AvroValueContainer(IEntityType tName, object[] rData)
    {
        EntityName = tName.Name;
        ClrType = tName.ClrType?.ToAssemblyQualified()!;
        Data = [];
        foreach (var item in tName.GetProperties())
        {
            int index = item.GetIndex();
            (NativeTypeMapper.ManagedTypes, bool) _type = NativeTypeMapper.GetValue(item.ClrType!);
            object? value = _type.Item1 switch
            {
                NativeTypeMapper.ManagedTypes.Guid => _type.Item2 ? (rData[index] as Guid?)?.ToString()!
                                                                  : ((Guid)rData[index]).ToString(),
                NativeTypeMapper.ManagedTypes.DateTime => _type.Item2 ? (rData[index] as DateTime?)?.ToString("O")!
                                                                      : ((DateTime)rData[index]).ToString("O"),
                NativeTypeMapper.ManagedTypes.DateTimeOffset => _type.Item2 ? (rData[index] as DateTimeOffset?)?.ToString("O")!
                                                                            : ((DateTimeOffset)rData[index]).ToString("O"),
                NativeTypeMapper.ManagedTypes.Char => _type.Item2 ? (rData[index] as char?)?.ToString(CultureInfo.InvariantCulture)
                                                                  : ((char)rData[index]).ToString(CultureInfo.InvariantCulture),
                NativeTypeMapper.ManagedTypes.SByte => _type.Item2 ? (rData[index] as sbyte?)?.ToString(CultureInfo.InvariantCulture)
                                                                   : ((sbyte)rData[index]).ToString(CultureInfo.InvariantCulture),
                NativeTypeMapper.ManagedTypes.UShort => _type.Item2 ? (rData[index] as ushort?)?.ToString(CultureInfo.InvariantCulture)
                                                                    : ((ushort)rData[index]).ToString(CultureInfo.InvariantCulture),
                NativeTypeMapper.ManagedTypes.UInt => _type.Item2 ? (rData[index] as uint?)?.ToString(CultureInfo.InvariantCulture)
                                                                  : ((uint)rData[index]).ToString(CultureInfo.InvariantCulture),
                NativeTypeMapper.ManagedTypes.ULong => _type.Item2 ? (rData[index] as ulong?)?.ToString(CultureInfo.InvariantCulture)
                                                                   : ((ulong)rData[index]).ToString(CultureInfo.InvariantCulture),
                NativeTypeMapper.ManagedTypes.Decimal => _type.Item2 ? (rData[index] as decimal?)?.ToString("G29", CultureInfo.InvariantCulture)
                                                                     : ((decimal)rData[index]).ToString("G29", CultureInfo.InvariantCulture),
                _ => rData[index],
            };
            var pRecord = new PropertyDataRecord
            {
                ManagedType = (int)_type.Item1,
                SupportNull = _type.Item2,
                PropertyIndex = index,
                PropertyName = item.Name,
                ClrType = item.ClrType?.ToAssemblyQualified(),
                Value = value
            };
            Data.Add(pRecord);
        }
    }
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
            switch ((NativeTypeMapper.ManagedTypes)Data[i].ManagedType)
            {
                case NativeTypeMapper.ManagedTypes.Guid:
                    {
                        if (array[i] != null)
                        {
                            if (!Guid.TryParse(array[i] as string, out Guid guid)) throw new InvalidCastException($"Unable to convert {array[i]} into {nameof(Guid)}");
                            array[i] = guid;
                        }
                        else if (!Data[i].SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.Guid)}");
                    }
                    break;
                case NativeTypeMapper.ManagedTypes.DateTime:
                    {
                        if (array[i] != null)
                        {
                            if (!DateTime.TryParse(array[i] as string, out DateTime dt)) throw new InvalidCastException($"Unable to convert {array[i]} into {nameof(DateTime)}");
                            array[i] = dt;
                        }
                        else if (!Data[i].SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.DateTime)}");
                    }
                    break;
                case NativeTypeMapper.ManagedTypes.DateTimeOffset:
                    {
                        if (array[i] != null)
                        {
                            if (!DateTimeOffset.TryParse(array[i] as string, out DateTimeOffset dto)) throw new InvalidCastException($"Unable to convert {array[i]} into {nameof(DateTimeOffset)}");
                            array[i] = dto;
                        }
                        else if (!Data[i].SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.DateTimeOffset)}");
                    }
                    break;
                case NativeTypeMapper.ManagedTypes.Char:
                    {
                        if (array[i] != null)
                        {
                            if (!char.TryParse(array[i] as string, out char dec)) throw new InvalidCastException($"Unable to convert {array[i]} into {nameof(System.Char)}");
                            array[i] = dec;
                        }
                        else if (!Data[i].SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.Char)}");
                    }
                    break;
                case NativeTypeMapper.ManagedTypes.SByte:
                    {
                        if (array[i] != null)
                        {
                            if (!sbyte.TryParse(array[i] as string, out sbyte dec)) throw new InvalidCastException($"Unable to convert {array[i]} into {nameof(System.SByte)}");
                            array[i] = dec;
                        }
                        else if (!Data[i].SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.SByte)}");
                    }
                    break;
                case NativeTypeMapper.ManagedTypes.UShort:
                    {
                        if (array[i] != null)
                        {
                            if (!ushort.TryParse(array[i] as string, out ushort dec)) throw new InvalidCastException($"Unable to convert {array[i]} into {nameof(System.UInt16)}");
                            array[i] = dec;
                        }
                        else if (!Data[i].SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.UInt16)}");
                    }
                    break;
                case NativeTypeMapper.ManagedTypes.UInt:
                    {
                        if (array[i] != null)
                        {
                            if (!uint.TryParse(array[i] as string, out uint dec)) throw new InvalidCastException($"Unable to convert {array[i]} into {nameof(System.UInt32)}");
                            array[i] = dec;
                        }
                        else if (!Data[i].SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.UInt32)}");
                    }
                    break;
                case NativeTypeMapper.ManagedTypes.ULong:
                    {
                        if (array[i] != null)
                        {
                            if (!ulong.TryParse(array[i] as string, out ulong dec)) throw new InvalidCastException($"Unable to convert {array[i]} into {nameof(System.UInt64)}");
                            array[i] = dec;
                        }
                        else if (!Data[i].SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.UInt64)}");
                    }
                    break;
                case NativeTypeMapper.ManagedTypes.Decimal:
                    {
                        if (array[i] != null)
                        {
                            if (!decimal.TryParse(array[i] as string, out decimal dec)) throw new InvalidCastException($"Unable to convert {array[i]} into {nameof(System.Decimal)}");
                            array[i] = dec;
                        }
                        else if (!Data[i].SupportNull) throw new InvalidCastException($"Unable to manage null values with {nameof(System.Decimal)}");
                    }
                    break;
                default:
                    break;
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
                Infrastructure.KafkaDbContext.ReportString($"Time to GetData with length {Data.Count}: {fullSw.Elapsed} - new array took: {newSw.Elapsed} - Iteration took: {iterationSw.Elapsed}");
            }
        }
#endif
    }
    /// <inheritdoc/>
    public IReadOnlyDictionary<int, string> GetProperties()
    {
        Dictionary<int, string> props = new();
        for (int i = 0; i < Data.Count; i++)
        {
            props.Add(Data[i].PropertyIndex, Data[i].PropertyName);
        }
        return props;
    }
}
