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

public static class CallerInfo
{
    public static string CallSite(
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => $"({member} in {Path.GetFileName(file)}:{line}) ";
}

/// <summary>
///     KEFCore specific extension methods for <see cref="IDiagnosticsLogger" />.
/// </summary>
public static class KEFCoreLoggerExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogCritical(this IDiagnosticsLogger logger, string callSite, Exception exception, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogCritical(callSite, exception, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogCritical(this ILogger logger, string callSite, Exception exception, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Critical))
        {
            logger.LogCritical(exception, callSite + message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogCritical(this IDiagnosticsLogger logger, string callSite, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogCritical(callSite, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogCritical(this ILogger logger, string callSite, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Critical))
        {
            logger.LogCritical(callSite + message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogError(this IDiagnosticsLogger logger, string callSite, Exception exception, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogError(callSite, exception, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogError(this ILogger logger, string callSite, Exception exception, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(exception, callSite + message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogError(this IDiagnosticsLogger logger, string callSite, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogError(callSite, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogError(this ILogger logger, string callSite, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(callSite + message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogWarning(this IDiagnosticsLogger logger, string callSite, Exception exception, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogWarning(callSite, exception, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogWarning(this ILogger logger, string callSite, Exception exception, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(exception, callSite + message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogWarning(this IDiagnosticsLogger logger, string callSite, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogWarning(callSite, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogWarning(this ILogger logger, string callSite, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(callSite + message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogInformation(this IDiagnosticsLogger logger, string callSite, Exception exception, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogInformation(callSite, exception, callSite, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogInformation(this ILogger logger, string callSite, Exception exception, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(exception, callSite + message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogInformation(this IDiagnosticsLogger logger, string callSite, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogInformation(callSite, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogInformation(this ILogger logger, string callSite, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(callSite + message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogDebug(this IDiagnosticsLogger logger, string callSite, Exception exception, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogDebug(callSite, exception, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogDebug(this ILogger logger, string callSite, Exception exception, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(exception, callSite + message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogDebug(this IDiagnosticsLogger logger, string callSite, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogDebug(callSite, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogDebug(this ILogger logger, string callSite, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(callSite + message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogTrace(this IDiagnosticsLogger logger, string callSite, Exception exception, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogTrace(callSite, exception, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogTrace(this ILogger logger, string callSite, Exception exception, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace(exception, callSite + message, args);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogTrace(this IDiagnosticsLogger logger, string callSite, string? message, params object?[] args)
    {
        logger?.Logger.CheckAndLogTrace(callSite, message, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckAndLogTrace(this ILogger logger, string callSite, string? message, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace(callSite + message, args);
        }
    }
}
