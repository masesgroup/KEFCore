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
    /// <param name="tName">The <see cref="IEntityType"/> requesting the <see cref="ProtobufValueContainer{TKey}"/> for <paramref name="propertyValues"/></param>
    /// <param name="properties">The set of <see cref="IProperty"/> deducted from <see cref="IEntityType.GetProperties"/>, if <see langword="null"/> the implmenting instance of <see cref="IValueContainer{T}"/> shall deduct it</param>
    /// <param name="propertyValues">The indexed data, built from EFCore, to be stored in the <see cref="ProtobufValueContainer{TKey}"/> associated to <paramref name="properties"/></param>
    /// <param name="complexProperties">The set of <see cref="IComplexProperty"/> deducted from <see cref="ITypeBase.GetComplexProperties"/>, if <see langword="null"/> the implementing instance of <see cref="IValueContainer{T}"/> does not process them</param>
    /// <param name="complexPropertyValues">The indexed data, built from EFCore, to be stored in the <see cref="ProtobufValueContainer{TKey}"/> associated to <paramref name="complexProperties"/></param>
    /// <param name="complexTypeFactory">The instance of <see cref="IComplexTypeConverterFactory"/> will manage strong type conversion</param>
    /// <remarks>This constructor is mandatory and it is used from KEFCore to request a <see cref="ProtobufValueContainer{TKey}"/></remarks>
    public ProtobufValueContainer(IEntityType tName, IProperty[]? properties, object?[] propertyValues, IComplexProperty[]? complexProperties = null, object?[]? complexPropertyValues = null, IComplexTypeConverterFactory? complexTypeFactory = null)
    {
        properties ??= [.. tName.GetProperties()];
        _innerMessage = new ValueContainer
        {
            EntityName = tName.Name,
            ClrType = tName.ClrType?.ToAssemblyQualified()!
        };
        _innerMessage.Data.Clear();
        foreach (var item in properties)
        {
            int index = item.GetIndex();
            var _type = NativeTypeMapper.GetValue(item.ClrType!);
            var pRecord = new PropertyDataRecord
            {
                PropertyName = item.Name,
                ClrType = _type.Item1 == NativeTypeMapper.ManagedTypes.Undefined ? item.ClrType?.ToAssemblyQualified() : string.Empty,
                Value = new GenericValue(_type, ref propertyValues[index]!)
            };
            _innerMessage.Data.Add(pRecord);
        }
        if (complexProperties != null && complexPropertyValues != null)
        {
            foreach (var item in complexProperties)
            {
                int index = item.GetIndex();
                var pRecord = new PropertyDataRecord
                {
                    PropertyName = item.Name,
                    ClrType = item.ClrType?.ToAssemblyQualified(),
                    Value = new GenericValue((NativeTypeMapper.ManagedTypes.ComplexType, false), ref complexPropertyValues[index]!, item, complexTypeFactory)
                };
                _innerMessage.Data.Add(pRecord);
            }
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
    public void GetData(IEntityType tName, IProperty[]? properties, IComplexProperty[]? complexProperties, ref object[] allPropertyValues, IComplexTypeConverterFactory? complexTypeFactory = null)
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
            if (_innerMessage.Data == null) { return; }
#if DEBUG_PERFORMANCE
            newSw.Start();
#endif
            allPropertyValues = new object[properties.Length + (complexProperties != null ? complexProperties.Length : 0)];
#if DEBUG_PERFORMANCE
            newSw.Stop();
            iterationSw.Start();
#endif
            for (int i = 0; i < _innerMessage.Data.Count; i++)
            {
                var item = _innerMessage.Data[i];
                if (item == null) continue;
                IPropertyBase? prop = item.Value.ManagedType == (int)NativeTypeMapper.ManagedTypes.ComplexType
                    ? tName.FindComplexProperty(item.PropertyName!)
                    : tName.FindProperty(item.PropertyName!);
                if (prop == null) continue; // a property was removed from the schema 
                item.Value.GetContent(prop, complexTypeFactory, ref allPropertyValues[i]!);
            }
#if DEBUG_PERFORMANCE
            iterationSw.Stop();
            fullSw.Stop();
        }
        finally
        {
            if (Internal.DebugPerformanceHelper.TraceEntityTypeDataStorageGetData)
            {
                Internal.DebugPerformanceHelper.ReportString($"Time to GetData with length {_innerMessage.Data?.Count}: {fullSw.Elapsed} - new array took: {newSw.Elapsed} - Iteration took: {iterationSw.Elapsed}");
            }
        }
#endif
    }
    /// <inheritdoc/>
    public IDictionary<string, object?> GetProperties(IComplexTypeConverterFactory? complexTypeFactory)
    {
        object? value = null;
        Dictionary<string, object?> props = [];
        foreach (var item in _innerMessage.Data)
        {
            item.Value.GetContent(null, complexTypeFactory: complexTypeFactory, ref value!);
            props.Add(item.PropertyName, value);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(props);
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
