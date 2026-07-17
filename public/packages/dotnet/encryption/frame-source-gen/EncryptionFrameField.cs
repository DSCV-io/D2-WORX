// -----------------------------------------------------------------------
// <copyright file="EncryptionFrameField.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionFrame.SourceGen;

/// <summary>
/// One encryption-frame field entry parsed from
/// <c>contracts/encryption-frame/encryption-frame.spec.json</c> or its
/// sealed sibling
/// <c>contracts/encryption-frame-sealed/encryption-frame-sealed.spec.json</c>
/// — the per-field shape is identical across both catalogs.
/// </summary>
/// <param name="ConstName">UPPER_SNAKE_CASE C# / TS constant identifier.</param>
/// <param name="Offset">Byte offset from frame start (-1 = variable).</param>
/// <param name="Length">Byte length (-1 = variable).</param>
/// <param name="Kind">How the decoder reads the field (one of the closed enum).</param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant.</param>
internal sealed record EncryptionFrameField(
    string ConstName,
    int Offset,
    int Length,
    string Kind,
    string Doc);
