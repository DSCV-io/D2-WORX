// -----------------------------------------------------------------------
// <copyright file="D2ProblemDetailsContextItems.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

/// <summary>
/// HttpContext.Items slot keys consumed by the
/// <c>D2ProblemDetailsCustomizer</c> when enriching ProblemDetails responses
/// flowing through ASP.NET Core's <c>IProblemDetailsService</c> pipeline
/// (path B). When middleware / handlers stash the originating
/// <see cref="DcsvIo.D2.Result.D2Result"/> under
/// <see cref="D2_RESULT"/>, the customizer reads it and populates the
/// RFC 7807 Shape A body D2Result-derived fields (<c>Type</c> + <c>Title</c>
/// + <c>Status</c> + <c>Extensions[d2_error_code]</c> +
/// <c>Extensions[d2_messages]</c> + <c>Extensions[d2_input_errors]</c>) from
/// spec-derived constants — keeping path A
/// (<c>D2ProblemDetailsExtensions.ToProblemDetails</c> in
/// <c>DcsvIo.D2.Auth.Http</c>) and path B byte-identical. <c>Instance</c>
/// and the <c>traceId</c> / <c>correlationId</c> extensions are populated
/// unconditionally (regardless of whether a <see cref="D2_RESULT"/> is
/// stashed) so the raw-exception path still surfaces correlation metadata.
/// </summary>
/// <remarks>
/// In-process slot keys are intentionally not cross-language wire formats —
/// they're .NET-internal HttpContext machinery. The constant lives here
/// (not in the cross-binding <c>in-process-keys</c> spec) because it's
/// HTTP-only AND aspnetcore-customizer-internal. Keeping it local avoids
/// pulling auth-abstractions transitive deps into aspnetcore consumers who
/// don't already need them.
/// </remarks>
public static class D2ProblemDetailsContextItems
{
    /// <summary>
    /// HttpContext.Items key under which middleware / handlers stash the
    /// originating <see cref="DcsvIo.D2.Result.D2Result"/> failure so the
    /// <c>D2ProblemDetailsCustomizer</c> can populate the RFC 7807
    /// ProblemDetails body from spec-derived constants. Double-underscore
    /// prefix marks the key as framework-internal (consumers should never
    /// reference this key directly — populate via the
    /// <c>SetD2Result</c> extension wrapper).
    /// </summary>
    public const string D2_RESULT = "__d2_result";
}
