// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsSpec.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.EncryptionDomains.SourceGen;

using System.Collections.Immutable;

/// <summary>Parsed shape of the encryption-domains spec file.</summary>
/// <param name="Domains">Every encryption-domain entry declared in the spec.</param>
internal sealed record EncryptionDomainsSpec(
    ImmutableArray<EncryptionDomainEntry> Domains);
