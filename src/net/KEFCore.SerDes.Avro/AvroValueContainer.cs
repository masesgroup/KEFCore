/*
*  Copyright 2024 MASES s.r.l.
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

using Avro;

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
        ClrType = tName.ClrType.FullName!;
        Data = new List<PropertyDataRecord>();
        foreach (var item in tName.GetProperties())
        {
            int index = item.GetIndex();
            var pRecord = new PropertyDataRecord
            {
                PropertyIndex = index,
                PropertyName = item.Name,
                ClrType = item.ClrType?.FullName,
                Value = rData[index]
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
