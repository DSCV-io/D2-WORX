// -----------------------------------------------------------------------
// <copyright file="ErrorCodeEntry.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.ErrorCodes.SourceGen;

/// <summary>
/// One error-code entry parsed from
/// <c>contracts/auth-error-codes/auth-error-codes.spec.json</c>.
/// </summary>
/// <param name="Code">
/// Wire-format error code (SCREAMING_SNAKE, <c>AUTH_*</c>-prefixed). Becomes
/// the emitted <c>AuthErrorCodes</c> constant value AND the <c>d2_error_code</c>
/// tag value seen on the wire.
/// </param>
/// <param name="HttpStatus">
/// HTTP status the failure surfaces with (today: <c>401</c> or <c>503</c>).
/// </param>
/// <param name="Category">
/// Closed enum: <c>validation_failure</c> / <c>infrastructure_unavailable</c>
/// / <c>policy_denied</c>. Drives both the emitted factory shape (which
/// <c>D2Result</c> semantic factory + whether a typed overload is emitted)
/// and telemetry classification.
/// </param>
/// <param name="UserMessageKey">
/// TK key reference (e.g. <c>TK.Auth.Errors.UNAUTHORIZED</c>) emitted as the
/// <c>messages</c> argument on the generated factory.
/// </param>
/// <param name="FactoryName">
/// PascalCase symbol for the generated factory method (e.g. <c>BearerMissing</c>).
/// </param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant + factory.</param>
internal sealed record ErrorCodeEntry(
    string Code,
    int HttpStatus,
    string Category,
    string UserMessageKey,
    string FactoryName,
    string Doc);
