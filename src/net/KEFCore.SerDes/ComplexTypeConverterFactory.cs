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

using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Collections.Concurrent;

namespace MASES.EntityFrameworkCore.KNet.Serialization
{
    /// <summary>
    /// Thread-safe implementation of <see cref="IComplexTypeConverterFactory"/>
    /// </summary>
    public class ComplexTypeConverterFactory(IDiagnosticsLogger<DbLoggerCategory.Infrastructure> loggingOptions) : IComplexTypeConverterFactory
    {
        readonly IDiagnosticsLogger<DbLoggerCategory.Infrastructure> _loggingOptions = loggingOptions;
        readonly ConcurrentDictionary<Type, IComplexTypeConverter> _converters = new();
        /// <inheritdoc/>
        public void Register(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            foreach (var type in assembly.ExportedTypes.Where(type => type.GetInterface(nameof(IComplexTypeConverter)) != null))
            {
                IComplexTypeConverter converter = (IComplexTypeConverter)Activator.CreateInstance(type)!;
                Register(converter);
            }
        }
        /// <inheritdoc/>
        public void Register(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            if (type.GetInterface(nameof(IComplexTypeConverter)) != null)
            {
                IComplexTypeConverter converter = (IComplexTypeConverter)Activator.CreateInstance(type)!;
                Register(converter);
            }
            else throw new InvalidOperationException($"Trying to register a type {type} that does not support {nameof(IComplexTypeConverter)}");
        }
        /// <inheritdoc/>
        public void Register(IComplexTypeConverter converter)
        {
            ArgumentNullException.ThrowIfNull(converter);
            foreach (var item in converter.SupportedClrTypes.Where(item => !_converters.TryAdd(item, converter)))
            {
                _loggingOptions.Logger.LogWarning("Trying to register a type {Type} that was previously register.", item);
            }
        }

        /// <inheritdoc/>
        public bool TryGet(IPropertyBase? complexProperty, out IComplexTypeConverter? converter)
        {
            ArgumentNullException.ThrowIfNull(complexProperty);
            return _converters.TryGetValue(complexProperty?.ClrType!, out converter);
        }
        /// <inheritdoc/>
        public bool TryGet(Type complexPropertyType, out IComplexTypeConverter? converter)
        {
            ArgumentNullException.ThrowIfNull(complexPropertyType);
            return _converters.TryGetValue(complexPropertyType!, out converter);
        }
        /// <inheritdoc/>
        public bool TryGet(string complexPropertyType, out IComplexTypeConverter? converter)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(complexPropertyType);
            return _converters.TryGetValue(Type.GetType(complexPropertyType, true)!, out converter);
        }
    }
}
