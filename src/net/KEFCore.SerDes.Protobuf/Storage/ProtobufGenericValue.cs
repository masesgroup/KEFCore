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
using System.Globalization;

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
        public void GetContent(ref object? result)
        {
            var dt = DatetimeValue.ToDateTime();
            result = UtcValue ? dt : dt.ToLocalTime();
        }
    }

    public sealed partial class ComplexType
    {
        /// <summary>
        /// Initialize a <see cref="ComplexType"/> with <paramref name="input"/>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="input">The generic object to be stored</param>
        public ComplexType(System.Type type, object? input)
        {
            ClrtypeValue = type?.ToAssemblyQualified();

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
        /// <summary>
        /// Returns a the converted object
        /// </summary>
        /// <param name="clrType">The string representing <see cref="System.Type"/></param>
        /// <param name="result">The object stored in <see cref="ComplexType"/></param>
        public void GetContent(out string clrType, ref object? result)
        {
            clrType = ClrtypeValue;
            result = KindCase switch
            {
                KindOneofCase.StringValue => StringValue,
                KindOneofCase.BytesValue => BytesValue.Memory,
                _ => throw new InvalidOperationException("Protobuf ComplexType can manage only converted values expressed as string or byte[]."),
            };
        }
    }

    public sealed partial class GenericValue
    {
        /// <summary>
        /// Initializer for <paramref name="input"/>
        /// </summary>
        /// <param name="input">The value to insert</param>
        /// <exception cref="InvalidOperationException"></exception>
        public GenericValue(ref object? input) : this(NativeTypeMapper.GetValue(input?.GetType()), ref input)
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
        public GenericValue((WellKnownManagedTypes, bool) _type, ref object? input, IPropertyBase? property = null, IComplexTypeConverterFactory? complexTypeFactory = null)
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
            else if (_type.Item1 == WellKnownManagedTypes.ComplexType)
            {
                IComplexTypeConverter? complexTypeHook = null;
                complexTypeFactory?.TryGet(property, out complexTypeHook);
                if (complexTypeHook == null || !complexTypeHook.Convert(PreferredConversionType.Text, ref input!))
                {
                    input = JsonSupport.ValueContainer.Serialize(property!.ClrType, input!);
                    ManagedType = (int)WellKnownManagedTypes.ComplexTypeAsJson;
                }

                ComplextypeValue = new ComplexType(property!.ClrType, input);
            }
            else throw new InvalidOperationException($"{input.GetType()} is not managed.");
        }
        /// <summary>
        /// Returns the content of <see cref="GenericValue"/>
        /// </summary>
        public void GetContent(IPropertyBase? property, IComplexTypeConverterFactory? complexTypeFactory, ref object? result)
        {
            switch (KindCase)
            {
                case KindOneofCase.None:
                case KindOneofCase.NullValue:
                    result = null!;
                    break;
                case KindOneofCase.BoolValue:
                    result = BoolValue;
                    break;
                case KindOneofCase.CharValue:
                    result = CharValue[0];
                    break;
                case KindOneofCase.ByteValue:
                    result = (byte)ByteValue;
                    break;
                case KindOneofCase.SbyteValue:
                    result = (sbyte)SbyteValue;
                    break;
                case KindOneofCase.ShortValue:
                    result = (short)ShortValue;
                    break;
                case KindOneofCase.UshortValue:
                    result = (short)UshortValue;
                    break;
                case KindOneofCase.IntValue:
                    result = IntValue;
                    break;
                case KindOneofCase.UintValue:
                    result = UintValue;
                    break;
                case KindOneofCase.LongValue:
                    result = LongValue;
                    break;
                case KindOneofCase.UlongValue:
                    result = UlongValue;
                    break;
                case KindOneofCase.FloatValue:
                    result = FloatValue;
                    break;
                case KindOneofCase.DoubleValue:
                    result = DoubleValue;
                    break;
                case KindOneofCase.StringValue:
                    result = StringValue;
                    break;
                case KindOneofCase.GuidValue:
                    result = new Guid([.. GuidValue]);
                    break;
                case KindOneofCase.DatetimeValue:
                    DatetimeValue.GetContent(ref result);
                    break;
                case KindOneofCase.DatetimeoffsetValue:
                    result = DatetimeoffsetValue.ToDateTimeOffset();
                    break;
                case KindOneofCase.DecimalValue:
                    result = decimal.Parse(DecimalValue);
                    break;
                case KindOneofCase.BytesValue:
                    result = BytesValue.Memory;
                    break;
                case KindOneofCase.ComplextypeValue:
                    {
                        ComplextypeValue.GetContent(out var clrType, ref result);
                        if (complexTypeFactory != null)
                        {
                            bool converted = true;
                            if (property != null && complexTypeFactory.TryGet(property, out var complexTypeHook))
                            {
                                converted = complexTypeHook.ConvertBack(PreferredConversionType.Binary, ref result!);
                            }
                            else if (complexTypeFactory.TryGet(clrType, out complexTypeHook))
                            {
                                converted = complexTypeHook.ConvertBack(PreferredConversionType.Binary, ref result!);
                            }
                            else throw new InvalidCastException($"Cannot manage record value {result} as {clrType}.");

                            if (!converted) throw new InvalidOperationException($"Protobuf ComplexType {nameof(IComplexTypeConverter)} instance refused to manage input {result} for {clrType} type.");
                        }
                        else throw new InvalidOperationException($"Protobuf ComplexType cannot manage input {result} for type {clrType} without a proper {nameof(IComplexTypeConverterFactory)} instance.");
                    }
                    break;
                case KindOneofCase.ComplextypeasstringValue:
                    {
                        ComplextypeasstringValue.GetContent(out var clrType, ref result);
                        if (result is string str)
                        {
                            System.Type type = property?.ClrType ?? System.Type.GetType(clrType)!;
                            result = JsonSupport.ValueContainer.Deserialize(type, str);
                        }
                        else throw new InvalidCastException($"Cannot convert record value {result} into string.");
                    }
                    break;
                default: throw new InvalidOperationException($"KindCase {KindCase} is not managed.");
            }
        }
    }
}
