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

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage
{
    public sealed partial class GenericValue
    {
        /// <summary>
        /// Initializer for <paramref name="input"/>
        /// </summary>
        /// <param name="input">The value to insert</param>
        /// <exception cref="InvalidOperationException"></exception>
        public GenericValue(object input)
        {
            if (input is null)
            {
                NullValue = Google.Protobuf.WellKnownTypes.NullValue.NullValue;
            }
            else if (input is bool boolVal)
            {
                BoolValue = boolVal;
            }
            else if (input is byte byteVal)
            {
                ByteValue = byteVal;
            }
            else if (input is short shortVal)
            {
                ShortValue = shortVal;
            }
            else if (input is int intVal)
            {
                IntValue = intVal;
            }
            else if (input is long longVal)
            {
                LongValue = longVal;
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
                DatetimeValue = Timestamp.FromDateTime(dateTimeVal);
            }
            else if (input is DateTimeOffset dateTimeOffsetVal)
            {
                DatetimeoffsetValue = Timestamp.FromDateTimeOffset(dateTimeOffsetVal);
            }
            else throw new InvalidOperationException($"{input.GetType()} is not managed.");
        }
        /// <summary>
        /// Returns the content of <see cref="GenericValue"/>
        /// </summary>
        public object GetContent()
        {
            return KindCase switch
            {
                KindOneofCase.NullValue => null!,
                KindOneofCase.BoolValue => BoolValue,
                KindOneofCase.ByteValue => ByteValue,
                KindOneofCase.ShortValue => ShortValue,
                KindOneofCase.IntValue => IntValue,
                KindOneofCase.LongValue => LongValue,
                KindOneofCase.FloatValue => FloatValue,
                KindOneofCase.DoubleValue => DoubleValue,
                KindOneofCase.StringValue => StringValue,
                KindOneofCase.GuidValue => new Guid(GuidValue.ToArray()),
                KindOneofCase.DatetimeValue => DatetimeValue.ToDateTime(),
                KindOneofCase.DatetimeoffsetValue => DatetimeValue.ToDateTimeOffset(),
                _ => throw new InvalidOperationException($"{KindCase} is not managed."),
            };
        }
    }
}
