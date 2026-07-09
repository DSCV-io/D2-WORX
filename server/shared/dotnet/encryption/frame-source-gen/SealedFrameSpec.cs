// -----------------------------------------------------------------------
// <copyright file="SealedFrameSpec.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.EncryptionFrame.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of the sealed-frame spec file
/// (<c>contracts/encryption-frame-sealed/encryption-frame-sealed.spec.json</c>).
/// Field entries reuse <see cref="EncryptionFrameField"/> — the per-field
/// shape is identical across the symmetric and sealed catalogs.
/// </summary>
/// <param name="Version">
/// Current sealed frame format version (encoder embeds; decoder validates).
/// Always ≥ 2 — version 1 is the symmetric frame.
/// </param>
/// <param name="Fields">Per-field byte-layout entries.</param>
/// <param name="Constraints">Frame-level numeric constraints.</param>
internal sealed record SealedFrameSpec(
    int Version,
    ImmutableArray<EncryptionFrameField> Fields,
    SealedFrameConstraints Constraints);
