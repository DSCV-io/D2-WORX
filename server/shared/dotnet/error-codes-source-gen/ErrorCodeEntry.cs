// -----------------------------------------------------------------------
// <copyright file="ErrorCodeEntry.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ResultErrorCodes.SourceGen;

/// <summary>
/// One error-code entry parsed from
/// <c>contracts/error-codes/error-codes.spec.json</c>.
/// </summary>
/// <param name="Code">
/// Wire-format error code (SCREAMING_SNAKE). Becomes the emitted
/// <c>ErrorCodes</c> constant value AND the <c>d2_error_code</c>
/// tag value seen on the wire.
/// </param>
/// <param name="HttpStatus">
/// HTTP status the failure surfaces with (the supported set covers
/// every status the 15 shipping entries collectively use).
/// </param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant.</param>
internal sealed record ErrorCodeEntry(
    string Code,
    int HttpStatus,
    string Doc);
