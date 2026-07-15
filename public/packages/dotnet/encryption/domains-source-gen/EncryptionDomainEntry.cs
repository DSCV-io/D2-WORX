// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionDomains.SourceGen;

/// <summary>
/// One encryption-domain entry parsed from
/// <c>contracts/encryption-domains/encryption-domains.spec.json</c>.
/// </summary>
/// <param name="ConstName">UPPER_SNAKE_CASE C# / TS constant identifier.</param>
/// <param name="Value">Wire-format domain identifier (e.g. <c>audit</c>).</param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant.</param>
/// <param name="Mode">
/// Optional encryption mode literal (<c>symmetric</c> / <c>sealed</c>);
/// <c>null</c> when absent from the spec (defaults to symmetric).
/// </param>
/// <param name="ConsumerService">
/// Optional decrypting-recipient ServiceId; required iff <see cref="Mode"/>
/// is <c>sealed</c>, forbidden otherwise. <c>null</c> when absent.
/// </param>
internal sealed record EncryptionDomainEntry(
    string ConstName,
    string Value,
    string Doc,
    string? Mode = null,
    string? ConsumerService = null);
