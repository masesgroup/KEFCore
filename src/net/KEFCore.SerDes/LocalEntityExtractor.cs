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

#nullable enable

using MASES.KNet.Serialization;
using Org.Apache.Kafka.Common.Header;
using System.Collections.Concurrent;

namespace MASES.EntityFrameworkCore.KNet.Serialization;

interface ILocalEntityExtractor<TJVMKey, TJVMValueContainer>
{
    object GetEntity(string topic, TJVMKey recordKey, TJVMValueContainer recordValue, Headers? headers, bool throwUnmatch);
}

class LocalEntityExtractor<TKey, TValueContainer, TJVMKey, TJVMValueContainer, TKeySerDesSelectorType, TValueContainerSerDesSelectorType>
    : ILocalEntityExtractor<TJVMKey, TJVMValueContainer>
    where TKey : notnull
    where TValueContainer : class, IValueContainer<TKey>
    where TKeySerDesSelectorType : class, ISerDesSelector<TKey>, new()
    where TValueContainerSerDesSelectorType : class, ISerDesSelector<TValueContainer>, new()
{
    static readonly ConcurrentDictionary<(Type, Type), ISerDes<TKey, TJVMKey>> _keySerdeses = new();
    static readonly ConcurrentDictionary<(Type, Type), ISerDes<TValueContainer, TJVMValueContainer>> _valueSerdeses = new();

    private readonly ISerDes<TKey, TJVMKey>? _keySerdes;
    private readonly ISerDes<TValueContainer, TJVMValueContainer>? _valueSerdes;
    private readonly IComplexTypeConverterFactory? _complexTypeFactory;
    private readonly IModel? _model;

    public LocalEntityExtractor()
    {
        _keySerdes = _keySerdeses.GetOrAdd((typeof(TKeySerDesSelectorType), typeof(TJVMKey)),
            _ => new TKeySerDesSelectorType().NewSerDes<TJVMKey>());
        _valueSerdes = _valueSerdeses.GetOrAdd((typeof(TValueContainerSerDesSelectorType), typeof(TValueContainer)),
            _ => new TValueContainerSerDesSelectorType().NewSerDes<TJVMValueContainer>());
    }

    /// <param name="complexTypeFactory">
    /// Optional <see cref="IComplexTypeConverterFactory"/> for ComplexType deserialization.
    /// Obtain it from the KEFCore model builder outside of <see cref="DbContext.OnModelCreating"/>.
    /// </param>
    /// <param name="model">
    /// Optional <see cref="IModel"/> for accurate property mapping via <see cref="IEntityType"/>.
    /// When provided, <see cref="IEntityType"/> is resolved from the CLR type embedded in the record value.
    /// </param>
    public LocalEntityExtractor(IComplexTypeConverterFactory? complexTypeFactory, IModel? model = null) : this()
    {
        _complexTypeFactory = complexTypeFactory;
        _model = model;
    }

    public object GetEntity(string topic, TJVMKey recordKey, TJVMValueContainer recordValue, Headers? headers, bool throwUnmatch)
    {
        if (recordValue == null) throw new ArgumentNullException(nameof(recordValue), "Record value shall be available");

        TValueContainer valueContainer = _valueSerdes?.DeserializeWithHeaders(topic, headers, recordValue)!;
        var clrType = Type.GetType(valueContainer.ClrType, true) ?? throw new ArgumentException($"Cannot create an instance of {valueContainer.ClrType}");
        var entityType = (_model?.FindEntityType(clrType)) ?? throw new ArgumentException($"Cannot extract an IEntityType from {valueContainer.ClrType}");
        var metadata = new ValueContainerMetadata(entityType!);
        var newEntity = Activator.CreateInstance(clrType!);
        foreach (var property in valueContainer.GetProperties(metadata))
        {
            var propInfo = clrType.GetProperty(property.Key);
            if (propInfo != null)
            {
                if (propInfo.CanWrite)
                    propInfo.SetValue(newEntity, property.Value);
                else if (throwUnmatch)
                    throw new InvalidOperationException($"Unable to write property {property.Value} at index {property.Key} with {property.Value}");
            }
            else if (throwUnmatch)
                throw new InvalidOperationException($"Property {property.Value} not found in {valueContainer.ClrType}");
        }

        foreach (var property in valueContainer.GetComplexProperties(metadata, _complexTypeFactory))
        {
            var propInfo = clrType.GetProperty(property.Key);
            if (propInfo != null)
            {
                if (propInfo.CanWrite)
                    propInfo.SetValue(newEntity, property.Value);
                else if (throwUnmatch)
                    throw new InvalidOperationException($"Unable to write property {property.Value} at index {property.Key} with {property.Value}");
            }
            else if (throwUnmatch)
                throw new InvalidOperationException($"Property {property.Value} not found in {valueContainer.ClrType}");
        }

        return newEntity!;
    }
}
