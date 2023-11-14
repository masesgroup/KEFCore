/*
*  Copyright 2023 MASES s.r.l.
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

namespace MASES.EntityFrameworkCore.KNet.Serialization.Protobuf.Storage
{
    public sealed partial class PrimaryKeyType
    {
        /// <summary>
        /// Initializer
        /// </summary>
        public PrimaryKeyType(params object[] input)
        {
            this.Values.Clear();
            foreach (var item in input)
            {
                Values.Add(new GenericValue(item));
            }
        }
        /// <summary>
        /// Returns the content of <see cref="PrimaryKeyType"/>
        /// </summary>
        public object[] GetContent()
        {
            object[] values = new object[Values.Count];
            for (int i = 0; i < Values.Count; i++)
            {
                values[i] = Values[i].GetContent();
            }
            return values;
        }
    }

    public sealed partial class KeyContainer
    {
        /// <summary>
        /// Initializer
        /// </summary>
        public KeyContainer(params object[] input)
        {
            PrimaryKey = new PrimaryKeyType(input);
        }
        /// <summary>
        /// Returns the content of <see cref="KeyContainer"/>
        /// </summary>
        public object GetContent()
        {
            return PrimaryKey.GetContent();
        }
    }
}
