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

using System.Runtime.CompilerServices;

namespace MASES.EntityFrameworkCore.KNet.Extensions;

/// <summary>
///     KEFCore specific extension methods for <see cref="IDiagnosticsLogger" />.
/// </summary>
public static class KEFCoreLoggerExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogCritical(this IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, string? message, params object?[] args)
    {
        if (logger.Logger.IsEnabled(LogLevel.Critical))
        {
            logger.Logger.LogCritical(message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogError(this IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, Exception exception, string? message, params object?[] args)
    {
        if (logger.Logger.IsEnabled(LogLevel.Error))
        {
            logger.Logger.LogError(exception, message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogError(this IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, string? message, params object?[] args)
    {
        if (logger.Logger.IsEnabled(LogLevel.Error))
        {
            logger.Logger.LogError(message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogWarning(this IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, string? message, params object?[] args)
    {
        if (logger.Logger.IsEnabled(LogLevel.Warning))
        {
            logger.Logger.LogWarning(message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogInformation(this IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, string? message, params object?[] args)
    {
        if (logger.Logger.IsEnabled(LogLevel.Information))
        {
            logger.Logger.LogInformation(message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogDebug(this IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, string? message, params object?[] args)
    {
        if (logger.Logger.IsEnabled(LogLevel.Debug))
        {
            logger.Logger.LogDebug(message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogTrace(this IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger, string? message, params object?[] args)
    {
        if (logger.Logger.IsEnabled(LogLevel.Trace))
        {
            logger.Logger.LogTrace(message, args);
        }
    }
}
