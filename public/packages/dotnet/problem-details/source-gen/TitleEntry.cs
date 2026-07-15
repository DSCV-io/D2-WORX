// -----------------------------------------------------------------------
// <copyright file="TitleEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ProblemDetails.SourceGen;

/// <summary>
/// One title entry parsed from
/// <c>contracts/problem-details/problem-details.spec.json</c>.
/// </summary>
/// <param name="ConstName">
/// UPPER_SNAKE_CASE C# / TS constant identifier (e.g. <c>UNAUTHORIZED</c>).
/// Becomes the public field name on the emitted partial class
/// (prefixed with <c>TITLE_</c>).
/// </param>
/// <param name="HttpStatus">
/// HTTP status this title maps to (e.g. 401, 503). <c>null</c> marks the
/// fallback entry used when no httpStatus-specific row matches.
/// </param>
/// <param name="Value">
/// Wire-format title value emitted on the RFC 7807 ProblemDetails JSON
/// body (e.g. <c>Unauthorized</c>, <c>Service Unavailable</c>).
/// </param>
/// <param name="Doc">
/// XML <c>summary</c> text rendered on the emitted constant.
/// </param>
internal sealed record TitleEntry(string ConstName, int? HttpStatus, string Value, string Doc);
