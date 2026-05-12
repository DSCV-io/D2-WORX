// -----------------------------------------------------------------------
// <copyright file="SanitizedExceptionRender.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Telemetry;

using System.Diagnostics;

/// <summary>
/// PII-safe rendering of an <see cref="Exception"/> for log diagnostics.
/// Returns the type FullName and the first stack frame only — never
/// <see cref="Exception.Message"/>, since exception messages can carry
/// arbitrary content interpolated from runtime data (request URIs,
/// response bodies, configured secrets, JWT contents) that must not reach
/// the log pipeline.
/// </summary>
/// <remarks>
/// Mirrors the pattern shipped in <c>D2.Shared.Auth.Outbound.Telemetry.SanitizedExceptionRender</c>
/// — duplicated here to keep auth and auth-outbound dep-graphs independent.
/// </remarks>
internal static class SanitizedExceptionRender
{
    /// <summary>Returns the exception type's <c>FullName</c> (or <c>Name</c> fallback).</summary>
    /// <param name="ex">The exception to render.</param>
    /// <returns>The fully-qualified exception type name.</returns>
    public static string TypeName(Exception ex) =>
        ex.GetType().FullName ?? ex.GetType().Name;

    /// <summary>
    /// Returns "<c>{Method} at {File}:{Line}</c>" for the first stack frame,
    /// or <c>"&lt;no frame&gt;"</c> if no stack trace is available. Method +
    /// file path are developer-controlled; user input cannot influence
    /// either, so this is safe to log.
    /// </summary>
    /// <param name="ex">The exception to render.</param>
    /// <returns>The first stack-frame description.</returns>
    public static string FirstFrame(Exception ex)
    {
        if (ex.StackTrace is null) return "<no frame>";

        var trace = new StackTrace(ex, fNeedFileInfo: true);
        if (trace.FrameCount == 0) return "<no frame>";
        var frame = trace.GetFrame(0);
        if (frame is null) return "<no frame>";
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
