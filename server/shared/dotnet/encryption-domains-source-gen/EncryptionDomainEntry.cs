// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainEntry.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.EncryptionDomains.SourceGen;

/// <summary>
/// One encryption-domain entry parsed from
/// <c>contracts/encryption-domains/encryption-domains.spec.json</c>.
/// </summary>
/// <param name="ConstName">UPPER_SNAKE_CASE C# / TS constant identifier.</param>
/// <param name="Value">Wire-format domain identifier (e.g. <c>audit</c>).</param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant.</param>
internal sealed record EncryptionDomainEntry(string ConstName, string Value, string Doc);
