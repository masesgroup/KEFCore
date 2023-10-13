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

#nullable enable

namespace MASES.EntityFrameworkCore.KNet.Serialization;
/// <summary>
/// This is the main interface a class must implmenet to be a ValueContainer. More info <see href="https://masesgroup.github.io/KEFCore/articles/serialization.html">here</see>
/// </summary>
/// <typeparam name="T">It is the key <see cref="Type"/> passed from Entity Framework associated to the Entity data will be stored in the ValueContainer</typeparam>
public interface IValueContainer<in T> where T : notnull
{
    /// <summary>
    /// Returns back the raw data associated to the Entity
    /// </summary>
    /// <param name="tName">The <see cref="IEntityType"/> requesting to get the data back</param>
    /// <param name="array">The array of object to be filled in with the data stored in the ValueContainer</param>
    void GetData(IEntityType tName, ref object[] array);
}
