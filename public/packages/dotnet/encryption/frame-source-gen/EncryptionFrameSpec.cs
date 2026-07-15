// -----------------------------------------------------------------------
// <copyright file="EncryptionFrameSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionFrame.SourceGen;

using System.Collections.Immutable;

/// <summary>Parsed shape of the encryption-frame spec file.</summary>
/// <param name="Version">Current frame format version (encoder embeds; decoder validates).</param>
/// <param name="Fields">Per-field byte-layout entries.</param>
/// <param name="Constraints">Frame-level numeric constraints.</param>
internal sealed record EncryptionFrameSpec(
    int Version,
    ImmutableArray<EncryptionFrameField> Fields,
    EncryptionFrameConstraints Constraints);
