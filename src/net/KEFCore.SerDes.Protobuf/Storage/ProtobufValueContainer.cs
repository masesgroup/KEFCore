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

using Google.Protobuf;
using Google.Protobuf.Reflection;
using MASES.KNet.Serialization;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage;

/// <summary>
/// The default ValueContainer used from KEFCore
/// </summary>
/// <typeparam name="TKey">It is the key <see cref="Type"/> passed from Entity Framework associated to the Entity data will be stored in the <see cref="ProtobufValueContainer{TKey}"/></typeparam>
public class ProtobufValueContainer<TKey> : IMessage<ProtobufValueContainer<TKey>>, IValueContainer<TKey>
    where TKey : notnull
{
    readonly ValueContainer _innerMessage;

    /// <summary>
    /// Initialize a new instance of <see cref="ProtobufValueContainer{TKey}"/>
    /// </summary>
    /// <remarks>It is mainly used from the JSON serializer</remarks>
    public ProtobufValueContainer() { _innerMessage = new ValueContainer(); _innerMessage.EntityName = _innerMessage.ClrType = null!; }
    /// <summary>
    /// Initialize a new instance of <see cref="ProtobufValueContainer{TKey}"/>
    /// </summary>
    /// <remarks>It is mainly used from the JSON serializer</remarks>
    public ProtobufValueContainer(ValueContainer clone) { _innerMessage = clone.Clone(); }
    /// <summary>
    /// Initialize a new instance of <see cref="ProtobufValueContainer{TKey}"/>
    /// </summary>
    /// <remarks>It is mainly used from the JSON serializer</remarks>
    public ProtobufValueContainer(ProtobufValueContainer<TKey> clone) { _innerMessage = clone._innerMessage.Clone(); }
    /// <summary>
    /// Initialize a new instance of <see cref="ProtobufValueContainer{TKey}"/>
    /// </summary>
    /// <param name="tName">The <see cref="IEntityType"/> requesting the <see cref="ProtobufValueContainer{TKey}"/> for <paramref name="rData"/></param>
    /// <param name="rData">The data, built from EFCore, to be stored in the <see cref="ProtobufValueContainer{TKey}"/></param>
    /// <remarks>This constructor is mandatory and it is used from KEFCore to request a <see cref="ProtobufValueContainer{TKey}"/></remarks>
    public ProtobufValueContainer(IEntityType tName, object[] rData)
    {
        _innerMessage = new ValueContainer
        {
            EntityName = tName.Name,
            ClrType = tName.ClrType?.ToAssemblyQualified()!
        };
        _innerMessage.Data.Clear();
        foreach (var item in tName.GetProperties())
        {
            int index = item.GetIndex();
            var pRecord = new PropertyDataRecord
            {
                PropertyIndex = index,
                PropertyName = item.Name,
                ClrType = item.ClrType?.ToAssemblyQualified(),
                Value = new GenericValue(rData[index])
            };
            _innerMessage.Data.Add(pRecord);
        }
    }
    /// <inheritdoc/>
    public string EntityName => _innerMessage.EntityName;
    /// <inheritdoc/>
    public string ClrType => _innerMessage.ClrType;
    /// <inheritdoc/>
    public MessageDescriptor Descriptor => (_innerMessage as IMessage).Descriptor;
    /// <inheritdoc/>
    public int CalculateSize() => _innerMessage.CalculateSize();
    /// <inheritdoc/>
    public ProtobufValueContainer<TKey> Clone() => new(this);
    /// <inheritdoc/>
    public bool Equals(ProtobufValueContainer<TKey>? other) => _innerMessage.Equals(other);
    /// <inheritdoc/>
    public void MergeFrom(ProtobufValueContainer<TKey> message) => _innerMessage.MergeFrom(message._innerMessage);
    /// <inheritdoc/>
    public void MergeFrom(CodedInputStream input) => _innerMessage.MergeFrom(input);
    /// <inheritdoc/>
    public void WriteTo(CodedOutputStream output) => _innerMessage.WriteTo(output);

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
        if (_innerMessage.Data == null) { return; }
#if DEBUG_PERFORMANCE
            newSw.Start();
#endif
        array = new object[_innerMessage.Data.Count];
#if DEBUG_PERFORMANCE
            newSw.Stop();
            iterationSw.Start();
#endif
        for (int i = 0; i < _innerMessage.Data.Count; i++)
        {
            array[i] = _innerMessage.Data[i].Value.GetContent();
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
        for (int i = 0; i < _innerMessage.Data.Count; i++)
        {
            props.Add(_innerMessage.Data[i].PropertyIndex, _innerMessage.Data[i].PropertyName);
        }
        return props;
    }
    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is ProtobufValueContainer<TKey>)
        {
            return _innerMessage.Equals((obj as ProtobufValueContainer<TKey>)?._innerMessage);
        }

        return false;
    }
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return _innerMessage.GetHashCode();
    }
}
