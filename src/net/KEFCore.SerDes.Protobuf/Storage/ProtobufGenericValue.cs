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

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using MASES.KNet.Serialization;
using Org.W3c.Dom.Ls;
using System.Globalization;
using static MASES.EntityFrameworkCore.KNet.Serialization.NativeTypeMapper;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage
{
    public sealed partial class Datetime
    {
        /// <summary>
        /// Initialize a <see cref="Datetime"/> with <paramref name="dt"/>
        /// </summary>
        /// <param name="dt">The <see cref="System.DateTime"/></param>
        public Datetime(System.DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Unspecified) throw new InvalidOperationException($"Cannot operate on {nameof(DateTime)} with Kind {DateTimeKind.Unspecified}");
            DatetimeValue = Timestamp.FromDateTime(dt.ToUniversalTime());
            UtcValue = dt.Kind == DateTimeKind.Utc;
        }
        /// <summary>
        /// Returns a <see cref="System.DateTime"/>
        /// </summary>
        /// <returns>The <see cref="System.DateTime"/> in <see cref="Datetime"/></returns>
        public System.DateTime GetContent()
        {
            var dt = DatetimeValue.ToDateTime();
            return UtcValue ? dt : dt.ToLocalTime();
        }
    }

    public sealed partial class ComplexType
    {
        /// <summary>
        /// Initialize a <see cref="ComplexType"/> with <paramref name="input"/>
        /// </summary>
        /// <param name="property">The <see cref="IPropertyBase"/> to be converted</param>
        /// <param name="input">The generic object to be stored</param>
        /// <param name="complexTypeFactory">The optional <see cref="IComplexTypeConverterFactory"/> used for conversion</param>
        public ComplexType(IPropertyBase? property, ref object? input, IComplexTypeConverterFactory? complexTypeFactory = null)
        {
            ClrtypeValue = input?.GetType()?.ToAssemblyQualified();
            if (complexTypeFactory != null
                && (property != null ? complexTypeFactory.TryGet(property, out var complexTypeHook)
                                     : complexTypeFactory.TryGet(ClrtypeValue, out complexTypeHook)))
            {
                if (complexTypeHook!.Convert(ref input))
                {
                    if (input is string str)
                    {
                        StringValue = str;
                    }
                    else if (input is byte[] bArray)
                    {
                        BytesValue = ByteString.CopyFrom(bArray);
                    }
                    else throw new InvalidOperationException("Protobuf ComplexType can manage only converted values expressed as string or byte[].");
                }
                else throw new InvalidOperationException($"Protobuf ComplexType {nameof(IComplexTypeConverter)} instance refused to manage input {input} for {ClrtypeValue}.");
            }
            else throw new InvalidOperationException($"Protobuf ComplexType cannot manage {ClrtypeValue} without a proper {nameof(IComplexTypeConverter)} instance: complexTypeFactory is {complexTypeFactory}.");
        }
        /// <summary>
        /// Returns a the converted object
        /// </summary>
        /// <param name="property">The <see cref="IPropertyBase"/> to be converted</param>
        /// <param name="complexTypeFactory">The optional <see cref="IComplexTypeConverterFactory"/> used for conversion</param>
        /// <returns>The object stored in <see cref="ComplexType"/></returns>
        public object? GetContent(IPropertyBase? property = null, IComplexTypeConverterFactory? complexTypeFactory = null)
        {
            object? result = KindCase switch
            {
                KindOneofCase.StringValue => StringValue,
                KindOneofCase.BytesValue => BytesValue.Memory,
                _ => throw new InvalidOperationException("Protobuf ComplexType can manage only converted values expressed as string or byte[]."),
            };
            if (complexTypeFactory != null 
                && (property != null ? complexTypeFactory.TryGet(property, out var complexTypeHook) 
                                     : complexTypeFactory.TryGet(ClrtypeValue, out complexTypeHook)))
            {
                if (complexTypeHook!.ConvertBack(ref result))
                {
                    return result;
                }
                else throw new InvalidOperationException($"Protobuf ComplexType {nameof(IComplexTypeConverter)} instance refused to manage input {result} for {ClrtypeValue}.");
            }
            else throw new InvalidOperationException($"Protobuf ComplexType cannot manage {ClrtypeValue} without a proper {nameof(IComplexTypeConverter)} instance.");
        }
    }

    public sealed partial class GenericValue
    {
        /// <summary>
        /// Initializer for <paramref name="input"/>
        /// </summary>
        /// <param name="input">The value to insert</param>
        /// <exception cref="InvalidOperationException"></exception>
        public GenericValue(ref object input) : this(NativeTypeMapper.GetValue(input?.GetType()), ref input)
        {
        }

        /// <summary>
        /// Initializer for <paramref name="input"/>
        /// </summary>
        /// <param name="_type">The <see cref="System.Type"/></param>
        /// <param name="input">The value to insert</param>
        /// <param name="property">The <see cref="IPropertyBase"/> describing the type</param>
        /// <param name="complexTypeFactory"><see cref="IComplexTypeConverterFactory"/> to use for conversions</param>
        /// <exception cref="InvalidOperationException"></exception>
        public GenericValue((ManagedTypes, bool) _type, ref object? input, IPropertyBase? property = null, IComplexTypeConverterFactory? complexTypeFactory = null)
        {
            ManagedType = (int)_type.Item1;
            SupportNull = _type.Item2;

            if (input is null)
            {
                NullValue = Google.Protobuf.WellKnownTypes.NullValue.NullValue;
            }
            else if (input is bool boolVal)
            {
                BoolValue = boolVal;
            }
            else if (input is char charVal)
            {
                CharValue = charVal.ToString();
            }
            else if (input is byte byteVal)
            {
                ByteValue = byteVal;
            }
            else if (input is sbyte sbyteVal)
            {
                SbyteValue = sbyteVal;
            }
            else if (input is short shortVal)
            {
                ShortValue = shortVal;
            }
            else if (input is ushort ushortVal)
            {
                UshortValue = ushortVal;
            }
            else if (input is int intVal)
            {
                IntValue = intVal;
            }
            else if (input is uint uintVal)
            {
                UintValue = uintVal;
            }
            else if (input is long longVal)
            {
                LongValue = longVal;
            }
            else if (input is ulong ulongVal)
            {
                UlongValue = ulongVal;
            }
            else if (input is float floatVal)
            {
                FloatValue = floatVal;
            }
            else if (input is double doubleVal)
            {
                DoubleValue = doubleVal;
            }
            else if (input is string stringVal)
            {
                StringValue = stringVal;
            }
            else if (input is Guid guidVal)
            {
                GuidValue = ByteString.CopyFrom(guidVal.ToByteArray());
            }
            else if (input is DateTime dateTimeVal)
            {
                DatetimeValue = new Datetime(dateTimeVal);
            }
            else if (input is DateTimeOffset dateTimeOffsetVal)
            {
                DatetimeoffsetValue = Timestamp.FromDateTimeOffset(dateTimeOffsetVal);
            }
            else if (input is decimal decimalVal)
            {
                DecimalValue = decimalVal.ToString("G29", CultureInfo.InvariantCulture);
            }
            else if (input is byte[] bytes)
            {
                BytesValue = ByteString.CopyFrom(bytes);
            }
            else if (_type.Item1 == NativeTypeMapper.ManagedTypes.ComplexType)
            {
                ComplextypeValue = new ComplexType(property, ref input, complexTypeFactory);
            }
            else throw new InvalidOperationException($"{input.GetType()} is not managed.");
        }
        /// <summary>
        /// Returns the content of <see cref="GenericValue"/>
        /// </summary>
        public object? GetContent(IPropertyBase? property, IComplexTypeConverterFactory? complexTypeFactory)
        {
            return KindCase switch
            {
                KindOneofCase.NullValue => null!,
                KindOneofCase.BoolValue => BoolValue,
                KindOneofCase.CharValue => CharValue[0],
                KindOneofCase.ByteValue => (byte)ByteValue,
                KindOneofCase.SbyteValue => (sbyte)SbyteValue,
                KindOneofCase.ShortValue => (short)ShortValue,
                KindOneofCase.UshortValue => (short)UshortValue,
                KindOneofCase.IntValue => IntValue,
                KindOneofCase.UintValue => UintValue,
                KindOneofCase.LongValue => LongValue,
                KindOneofCase.UlongValue => UlongValue,
                KindOneofCase.FloatValue => FloatValue,
                KindOneofCase.DoubleValue => DoubleValue,
                KindOneofCase.StringValue => StringValue,
                KindOneofCase.GuidValue => new Guid([.. GuidValue]),
                KindOneofCase.DatetimeValue => DatetimeValue.GetContent(),
                KindOneofCase.DatetimeoffsetValue => DatetimeoffsetValue.ToDateTimeOffset(),
                KindOneofCase.DecimalValue => decimal.Parse(DecimalValue),
                KindOneofCase.BytesValue => BytesValue.Memory,
                KindOneofCase.ComplextypeValue => ComplextypeValue.GetContent(property, complexTypeFactory),
                _ => throw new InvalidOperationException($"{KindCase} is not managed."),
            };
        }
    }
}
