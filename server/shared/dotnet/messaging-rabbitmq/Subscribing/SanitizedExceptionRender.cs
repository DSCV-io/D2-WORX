// -----------------------------------------------------------------------
// <copyright file="SanitizedExceptionRender.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging.RabbitMq.Subscribing;

using System.Diagnostics;

/// <summary>
/// PII-safe rendering of an <see cref="Exception"/> for log + DLQ
/// diagnostics. Returns the type FullName and the first stack frame only —
/// never <c>exception.Message</c> (handler code can interpolate user input
/// into messages, and the broker / log pipeline must not see those
/// strings).
/// </summary>
internal static class SanitizedExceptionRender
{
    /// <summary>Returns the exception type's <c>FullName</c> (or <c>Name</c>
    /// fallback). Safe to log / attach to a broker header.</summary>
    /// <param name="ex">Exception.</param>
    public static string TypeName(Exception ex) =>
        ex.GetType().FullName ?? ex.GetType().Name;

    /// <summary>Returns "<c>{Method} at {File}:{Line}</c>" for the first
    /// stack frame, or <c>null</c> if no stack trace is available. Method +
    /// file path are developer-controlled; user input cannot influence
    /// either, so this is safe to log.</summary>
    /// <param name="ex">Exception.</param>
    public static string? FirstFrame(Exception ex)
    {
        if (ex.StackTrace is null) return null;

        var trace = new StackTrace(ex, fNeedFileInfo: true);
        if (trace.FrameCount == 0) return null;
        var frame = trace.GetFrame(0);
        if (frame is null) return null;
        var method = frame.GetMethod();
        var methodName = method is null
            ? "<unknown>"
            : $"{method.DeclaringType?.FullName ?? "<global>"}.{method.Name}";
        var file = frame.GetFileName();
        var line = frame.GetFileLineNumber();
        return file is null
            ? methodName
            : $"{methodName} at {file}:{line}";
    }
}
