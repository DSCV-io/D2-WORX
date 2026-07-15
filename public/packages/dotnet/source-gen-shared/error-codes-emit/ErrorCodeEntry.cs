// -----------------------------------------------------------------------
// <copyright file="ErrorCodeEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

/// <summary>
/// One error-code entry parsed from a <c>*-error-codes.spec.json</c> catalog.
/// The superset shape across every catalog: the three constants-only fields
/// (<see cref="Code"/> / <see cref="HttpStatus"/> / <see cref="Doc"/>) are
/// always present; the four factory fields (<see cref="Category"/> /
/// <see cref="UserMessageKey"/> / <see cref="FactoryName"/> /
/// <see cref="FactoryShape"/>) are nullable — populated only for
/// factory-bearing catalogs (e.g. auth) and left <c>null</c> for the generic
/// constants-only catalog.
/// </summary>
/// <param name="Code">
/// Wire-format error code (SCREAMING_SNAKE; per-domain catalogs are
/// domain-prefix-enforced, e.g. <c>AUTH_*</c>). Becomes the emitted
/// constant value AND the <c>d2_error_code</c> tag value seen on the wire.
/// </param>
/// <param name="HttpStatus">HTTP status the failure surfaces with.</param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant + factory.</param>
/// <param name="Category">
/// Closed semantic/telemetry classification (e.g. <c>validation_failure</c>);
/// <c>null</c> for the generic constants-only catalog.
/// </param>
/// <param name="UserMessageKey">
/// TK key reference (e.g. <c>TK.Auth.Errors.UNAUTHORIZED</c>) emitted as the
/// <c>messages</c> argument on the generated factory; <c>null</c> for the
/// generic constants-only catalog.
/// </param>
/// <param name="FactoryName">
/// PascalCase symbol for the generated factory method (e.g.
/// <c>BearerMissing</c>); <c>null</c> for the generic constants-only catalog.
/// </param>
/// <param name="FactoryShape">
/// Closed enum driving the generated factory's signature variant
/// (<c>standard</c> — the universal error-factory shape — or <c>none</c>);
/// <c>null</c> for the generic constants-only catalog.
/// </param>
/// <param name="Deprecated">
/// When <see langword="true"/>, the entry is deprecated: the emitted constant +
/// factory carry an <c>[Obsolete]</c> attribute built from
/// <see cref="DeprecatedReason"/> + <see cref="ReplacedBy"/>. Defaults to
/// <see langword="false"/> (active). The mere presence of the marker flips the
/// entry to deprecated; the entry is NEVER deleted.
/// </param>
/// <param name="DeprecatedReason">
/// Plain dev-facing English explaining why the code is deprecated; rendered
/// verbatim into the <c>[Obsolete("...")]</c> message. NOT a TK key. Present
/// only when <see cref="Deprecated"/> is <see langword="true"/>.
/// </param>
/// <param name="ReplacedBy">
/// Wire code of the successor entry (e.g. <c>RESOURCE_NOT_FOUND</c>). When
/// present, the emitted <c>[Obsolete]</c> message appends
/// <c>" Use {ReplacedBy} instead."</c>. <see langword="null"/> for a pure
/// retirement with no replacement.
/// </param>
/// <param name="Sunset">
/// ISO-8601 date for the future RFC 8594 <c>Sunset</c> response header. Carried
/// on the contract now, consumed by the Edge response middleware when it
/// exists. Inert today (no emitter reads it).
/// </param>
internal sealed record ErrorCodeEntry(
    string Code,
    int HttpStatus,
    string Doc,
    string? Category = null,
    string? UserMessageKey = null,
    string? FactoryName = null,
    string? FactoryShape = null,
    bool Deprecated = false,
    string? DeprecatedReason = null,
    string? ReplacedBy = null,
    string? Sunset = null);
