// -----------------------------------------------------------------------
// <copyright file="SanitizedExceptionRender.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Diagnostics;

using System.Diagnostics;

/// <summary>
/// PII-safe rendering of an <see cref="Exception"/> for log + telemetry +
/// DLQ-header diagnostics. Returns the type FullName and the first stack
/// frame only — never <see cref="Exception.Message"/>, since exception
/// messages can carry arbitrary content interpolated from runtime data
/// (request URIs, response bodies, configured secrets, JWT contents, AMQP
/// connection URIs with embedded passwords, connection strings) that must
/// not reach the log pipeline / broker headers / metric tags.
/// </summary>
/// <remarks>
/// <para>
/// Canonical helper consumed by every lib whose
/// <c>[LoggerMessage]</c> delegates carry exception-derived strings —
/// <c>DcsvIo.D2.Auth</c>, <c>DcsvIo.D2.Auth.Outbound</c>,
/// <c>DcsvIo.D2.AspNetCore</c>, <c>DcsvIo.D2.Messaging.RabbitMq</c>, and
/// any future logging-pipeline consumer. Pair with the
/// no-<see cref="Exception"/>-parameter <c>[LoggerMessage]</c> contract
/// (enforced by per-lib reflection-based contract tests across each log
/// surface) to keep <see cref="Exception.Message"/> out of the log pipeline
/// at the type level.
/// </para>
/// </remarks>
public static class SanitizedExceptionRender
{
    /// <summary>Returns the exception type's <c>FullName</c> (or <c>Name</c> fallback).</summary>
    /// <param name="ex">The exception to render.</param>
    /// <returns>The fully-qualified exception type name; safe to log /
    /// attach to a broker header / record on a span.</returns>
    public static string TypeName(Exception ex) =>
        ex.GetType().FullName ?? ex.GetType().Name;

    /// <summary>
    /// Returns "<c>{Method} at {File}:{Line}</c>" for the first stack frame,
    /// or <c>"&lt;no frame&gt;"</c> if no stack trace is available. Method +
    /// file path are developer-controlled; user input cannot influence
    /// either, so this is safe to log.
    /// </summary>
    /// <param name="ex">The exception to render.</param>
    /// <returns>The first stack-frame description, or the
    /// <c>"&lt;no frame&gt;"</c> sentinel when no stack is available.
    /// Callers can string-interpolate the result without a null guard.</returns>
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
