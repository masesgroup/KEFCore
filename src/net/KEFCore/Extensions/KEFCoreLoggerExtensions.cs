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
    public static void CheckAndLogCritical(this IDiagnosticsLogger logger, Exception exception, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogCritical(exception, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogCritical(this ILogger logger, Exception exception, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Critical))
        {
            logger.LogCritical(exception, message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogCritical(this IDiagnosticsLogger logger, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogCritical(message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogCritical(this ILogger logger, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Critical))
        {
            logger.LogCritical(message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogError(this IDiagnosticsLogger logger, Exception exception, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogError(exception, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogError(this ILogger logger, Exception exception, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(exception, message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogError(this IDiagnosticsLogger logger, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogError(message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogError(this ILogger logger, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogWarning(this IDiagnosticsLogger logger, Exception exception, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogWarning(exception, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogWarning(this ILogger logger, Exception exception, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(exception, message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogWarning(this IDiagnosticsLogger logger, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogWarning(message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogWarning(this ILogger logger, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogInformation(this IDiagnosticsLogger logger, Exception exception, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogInformation(exception, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogInformation(this ILogger logger, Exception exception, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(exception, message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogInformation(this IDiagnosticsLogger logger, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogInformation(message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogInformation(this ILogger logger, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogDebug(this IDiagnosticsLogger logger, Exception exception, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogDebug(exception, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogDebug(this ILogger logger, Exception exception, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(exception, message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogDebug(this IDiagnosticsLogger logger, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogDebug(message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogDebug(this ILogger logger, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogTrace(this IDiagnosticsLogger logger, Exception exception, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogTrace(exception, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogTrace(this ILogger logger, Exception exception, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace(exception, message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogTrace(this IDiagnosticsLogger logger, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogTrace(message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogTrace(this ILogger logger, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace(message, args);
        }
    }
}
