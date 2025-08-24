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

using MASES.JCOBridge.C2JBridge;
using Org.Apache.Kafka.Streams.Errors;

namespace MASES.EntityFrameworkCore.KNet.Storage.Internal
{
    internal class KEFCoreStreamsUncaughtExceptionHandler(Func<JVMBridgeException, StreamsUncaughtExceptionHandler.StreamThreadExceptionResponse> onHandle) : StreamsUncaughtExceptionHandler
    {
        readonly Func<JVMBridgeException, StreamThreadExceptionResponse> _onHandle = onHandle;

        public override StreamThreadExceptionResponse Handle(JVMBridgeException arg0)
        {
            return _onHandle != null ? _onHandle.Invoke(arg0) : StreamThreadExceptionResponse.SHUTDOWN_APPLICATION;
        }
    }
}
