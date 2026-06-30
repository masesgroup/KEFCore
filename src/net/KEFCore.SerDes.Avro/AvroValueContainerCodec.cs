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

using global::Avro.IO;
using global::MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage;

// Direct codec for AvroValueContainer, replacing SpecificDefaultWriter/SpecificDefaultReader.
//
// Drop-in replacement points in AvroKEFCoreSerDes.cs (both BinaryRaw<TData> and
// BinaryBuffered<TData> nested classes, around lines ~652-757 and the analogous
// AvroKeyContainer blocks if the same approach is applied there too):
//
//   BEFORE:
//     SpecificWriter.Write(data, encoder);
//     ...
//     t = SpecificReader.Read(t!, decoder);
//
//   AFTER:
//     AvroValueContainerCodec.Write(data, encoder);
//     ...
//     t = AvroValueContainerCodec.Read(decoder, () => ValueContainerFactory<TData>.Create());
//
// Union branch order for "Value" confirmed from AvroValueContainer.avsc:
//   0: null, 1: boolean, 2: int, 3: long, 4: float, 5: double, 6: string, 7: bytes

namespace MASES.EntityFrameworkCore.KNet.Serialization.Avro.Storage
{
    /// <summary>
    /// Hand-written direct codec for <see cref="AvroValueContainer"/> and its nested
    /// <see cref="PropertyDataRecord"/> entries. Bypasses ISpecificRecord.Get(int)/Put(int, object),
    /// avoiding one boxing allocation per scalar field per property per entity.
    /// </summary>
    public static class AvroValueContainerCodec
    {
        // --- WRITE -----------------------------------------------------------

        public static void Write(AvroValueContainer container, Encoder encoder)
        {
            encoder.WriteString(container.EntityName);
            encoder.WriteString(container.ClrType);

            var data = container.Data;
            int count = data.Count;
            // Avro array encoding: a sequence of blocks, each with a non-zero item count
            // followed by that many items, terminated by a zero-count block.
            encoder.WriteArrayStart();
            encoder.SetItemCount(count);
            for (int i = 0; i < count; i++)
            {
                encoder.StartItem();
                WriteProperty(data[i], encoder);
            }
            encoder.WriteArrayEnd();
        }

        private static void WriteProperty(PropertyDataRecord record, Encoder encoder)
        {
            // optional int -> union [null, int]
            if (record.PropertyIndex.HasValue)
            {
                encoder.WriteUnionIndex(1);
                encoder.WriteInt(record.PropertyIndex.Value);
            }
            else
            {
                encoder.WriteUnionIndex(0);
                encoder.WriteNull();
            }

            encoder.WriteString(record.PropertyName);

            // plain int / bool — no union, no boxing already in the generated class,
            // but writing them directly here avoids the Get(int)/Put(int,object) round trip too.
            encoder.WriteInt(record.ManagedType);
            encoder.WriteBoolean(record.SupportNull);

            // optional string -> union [null, string]
            if (record.ClrType != null)
            {
                encoder.WriteUnionIndex(1);
                encoder.WriteString(record.ClrType);
            }
            else
            {
                encoder.WriteUnionIndex(0);
                encoder.WriteNull();
            }

            // Value union: null, boolean, int, long, float, double, string, bytes
            WriteValueUnion(record.Value, encoder);
        }

        private static void WriteValueUnion(object value, Encoder encoder)
        {
            switch (value)
            {
                case null:
                    encoder.WriteUnionIndex(0);
                    encoder.WriteNull();
                    break;
                case bool b:
                    encoder.WriteUnionIndex(1);
                    encoder.WriteBoolean(b);
                    break;
                case int i:
                    encoder.WriteUnionIndex(2);
                    encoder.WriteInt(i);
                    break;
                case long l:
                    encoder.WriteUnionIndex(3);
                    encoder.WriteLong(l);
                    break;
                case float f:
                    encoder.WriteUnionIndex(4);
                    encoder.WriteFloat(f);
                    break;
                case double d:
                    encoder.WriteUnionIndex(5);
                    encoder.WriteDouble(d);
                    break;
                case string s:
                    encoder.WriteUnionIndex(6);
                    encoder.WriteString(s);
                    break;
                case byte[] bytes:
                    encoder.WriteUnionIndex(7);
                    encoder.WriteBytes(bytes);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported Value type {value.GetType()}");
            }
        }

        // --- READ ------------------------------------------------------------

        public static T Read<T>(Decoder decoder, Func<T> factory) where T : AvroValueContainer
        {
            var container = factory();

            container.EntityName = decoder.ReadString();
            container.ClrType = decoder.ReadString();

            var list = new List<PropertyDataRecord>();
            for (long n = decoder.ReadArrayStart(); n != 0; n = decoder.ReadArrayNext())
            {
                for (long i = 0; i < n; i++)
                {
                    list.Add(ReadProperty(decoder));
                }
            }
            container.Data = list;

            return container;
        }

        private static PropertyDataRecord ReadProperty(Decoder decoder)
        {
            var record = new PropertyDataRecord();

            int idx = decoder.ReadUnionIndex();
            if (idx == 1)
            {
                record.PropertyIndex = decoder.ReadInt();
            }
            else
            {
                decoder.ReadNull();
                record.PropertyIndex = null;
            }

            record.PropertyName = decoder.ReadString();
            record.ManagedType = decoder.ReadInt();
            record.SupportNull = decoder.ReadBoolean();

            idx = decoder.ReadUnionIndex();
            if (idx == 1)
            {
                record.ClrType = decoder.ReadString();
            }
            else
            {
                decoder.ReadNull();
                record.ClrType = null;
            }

            record.Value = ReadValueUnion(decoder);

            return record;
        }

        private static object ReadValueUnion(Decoder decoder)
        {
            int branch = decoder.ReadUnionIndex();
            switch (branch)
            {
                case 0:
                    decoder.ReadNull();
                    return null;
                case 1:
                    return decoder.ReadBoolean();
                case 2:
                    return decoder.ReadInt();
                case 3:
                    return decoder.ReadLong();
                case 4:
                    return decoder.ReadFloat();
                case 5:
                    return decoder.ReadDouble();
                case 6:
                    return decoder.ReadString();
                case 7:
                    return decoder.ReadBytes();
                default:
                    throw new NotSupportedException($"Unsupported union branch {branch}");
            }
        }
    }
}
