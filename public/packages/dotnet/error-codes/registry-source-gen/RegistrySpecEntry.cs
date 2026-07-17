// -----------------------------------------------------------------------
// <copyright file="RegistrySpecEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

/// <summary>
/// A single error-code entry enriched with registry-level fields — the
/// source-gen counterpart to the runtime <c>ErrorCodeInfo</c> record that
/// the registry emitter produces. Carries all 8 fields the registry exposes:
/// the 7 spec fields (<see cref="Code"/> / <see cref="HttpStatus"/> /
/// <see cref="Category"/> / <see cref="UserMessageKey"/> /
/// <see cref="FactoryName"/> / <see cref="FactoryShape"/> / <see cref="Doc"/>)
/// plus the registry-derived <see cref="Domain"/> token.
/// </summary>
/// <param name="Code">Wire-format error code (SCREAMING_SNAKE).</param>
/// <param name="HttpStatus">HTTP status mapping for transport.</param>
/// <param name="Category">
/// Closed semantic/telemetry classification from the spec
/// (e.g. <c>validation_failure</c>, <c>not_found</c>).
/// </param>
/// <param name="UserMessageKey">
/// TK symbol-path reference from the spec
/// (e.g. <c>TK.Auth.Errors.UNAUTHORIZED</c>).
/// </param>
/// <param name="FactoryName">
/// PascalCase factory name from the spec (e.g. <c>BearerMissing</c>).
/// </param>
/// <param name="FactoryShape">
/// Closed factory-shape value from the spec
/// (<c>standard</c> — the universal error-factory shape — or <c>none</c>).
/// </param>
/// <param name="Doc">XML summary / JSDoc text for this code.</param>
/// <param name="Domain">
/// Domain token derived from the spec filename: the generic
/// <c>error-codes.spec.json</c> → <c>common</c>; a per-domain spec like
/// <c>auth-error-codes.spec.json</c> → <c>auth</c>.
/// </param>
/// <param name="SpecFileName">
/// The spec filename this entry was loaded from. Used for collision
/// diagnostics and domain derivation.
/// </param>
/// <param name="IsDeprecated">
/// <see langword="true"/> when the spec entry carries the deprecation marker
/// (<c>"deprecated": true</c>). Surfaced on the runtime <c>ErrorCodeInfo</c> so
/// telemetry / runtime consumers can observe deprecation state. Defaults to
/// <see langword="false"/> (active).
/// </param>
internal sealed record RegistrySpecEntry(
    string Code,
    int HttpStatus,
    string Category,
    string UserMessageKey,
    string FactoryName,
    string FactoryShape,
    string Doc,
    string Domain,
    string SpecFileName,
    bool IsDeprecated = false);
