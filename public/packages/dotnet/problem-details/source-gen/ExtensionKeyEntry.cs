// -----------------------------------------------------------------------
// <copyright file="ExtensionKeyEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ProblemDetails.SourceGen;

/// <summary>
/// One extension-key entry parsed from
/// <c>contracts/problem-details/problem-details.spec.json</c>.
/// </summary>
/// <param name="ConstName">
/// UPPER_SNAKE_CASE C# / TS constant identifier (e.g. <c>ERROR_CODE</c>).
/// Becomes the public field name on the emitted partial class.
/// </param>
/// <param name="Value">
/// Wire-format extension key emitted on the RFC 7807 ProblemDetails JSON
/// body (e.g. <c>d2_error_code</c>, <c>traceId</c>). The literal IS the
/// wire format.
/// </param>
/// <param name="Doc">
/// XML <c>summary</c> text rendered on the emitted constant.
/// </param>
internal sealed record ExtensionKeyEntry(string ConstName, string Value, string Doc);
