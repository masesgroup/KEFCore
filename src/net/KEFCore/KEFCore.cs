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

using MASES.KNet;

namespace MASES.EntityFrameworkCore.KNet
{
    /// <summary>
    /// This is the primary class shall be used to initialize the environment to use Entity Framework Core for Apache Kafka
    /// </summary>
    /// <example>
    /// The most simple way to use this class is to execute the following code at the beginning of the application:
    /// <code>
    /// KEFCore.CreateGlobalInstance();
    /// </code>
    /// The class reads configuration parameters in multiple ways: command line, environment variables and code override.
    /// To insert values in the code an user can create a custom class like the following and overrides the interested properties:
    /// <code>
    /// public class CustomKEFCore : KEFCore
    /// {
    ///     public override string JVMPath =&gt; "MySpecialPath";
    /// }
    /// </code>
    /// </example>
    public class KEFCore : KNetCore<KEFCore>
    {
#if DEBUG
        /// <inheritdoc/>
        public override bool EnableDebug => true;
        /// <inheritdoc/>
        public override bool LogClassPath => true;
#endif
        /// <summary>
        /// Set to <see langword="false"/> to disable Apache Kafka Streams caching, default is <see langword="true"/>
        /// </summary>
        /// <remarks>This value is read only once when application starts: if the backend uses Apache Kafka Streams (i.e. <see cref="MASES.EntityFrameworkCore.KNet.Infrastructure.KafkaDbContext.UseCompactedReplicator"/> is <see langword="false"/>), the value is read to understand how to manage Streams instances lifetime</remarks>
        public static bool PreserveInformationAcrossContexts { get; set; } = true;
    }
}
