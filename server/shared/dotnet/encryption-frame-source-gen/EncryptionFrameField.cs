// -----------------------------------------------------------------------
// <copyright file="EncryptionFrameField.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.EncryptionFrame.SourceGen;

/// <summary>
/// One encryption-frame field entry parsed from
/// <c>contracts/encryption-frame/encryption-frame.spec.json</c>.
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
