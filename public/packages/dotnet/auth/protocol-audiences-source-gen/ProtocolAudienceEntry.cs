// -----------------------------------------------------------------------
// <copyright file="ProtocolAudienceEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.ProtocolAudiences.SourceGen;

/// <summary>
/// One protocol-audience entry parsed from the spec.
/// </summary>
/// <param name="Name">
/// SCREAMING_SNAKE_CASE identifier emitted as a <c>public const string</c> on the
/// generated <c>WellKnownAudiences</c> class (e.g. <c>"D2_INTERNAL_AUDIENCE"</c> →
/// <c>WellKnownAudiences.D2_INTERNAL_AUDIENCE</c>). Must match
/// <c>^[A-Z][A-Z0-9_]*$</c>.
/// </param>
/// <param name="Value">
/// The bare-token <c>aud</c>-claim value (e.g. <c>"d2.internal"</c>). The const's
/// value is this string. Intentionally NOT URL-shaped — protocol audiences are
/// bare tokens, unlike the URL-shaped token-exchange targets in
/// <c>contracts/auth-audiences/audiences.spec.json</c>. Must be non-empty.
/// </param>
/// <param name="Description">
/// Free-form description rendered as XML doc on the emitted constant.
/// </param>
internal sealed record ProtocolAudienceEntry(
    string Name, string Value, string? Description);
