// -----------------------------------------------------------------------
// <copyright file="SeedCertificateAuthorityOutput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority;

/// <summary>
/// Result of a <c>SeedCertificateAuthority</c> execution.
/// </summary>
/// <param name="Seeded">
/// <see langword="true"/> when this call seeded the hierarchy;
/// <see langword="false"/> when an active hierarchy already existed (idempotent
/// no-op).
/// </param>
/// <param name="RootKid">
/// The seeded root CA key's kid, or <see langword="null"/> on a no-op.
/// </param>
/// <param name="IntermediateKid">
/// The seeded intermediate CA key's kid, or <see langword="null"/> on a no-op.
/// </param>
public sealed record SeedCertificateAuthorityOutput(
    bool Seeded, string? RootKid, string? IntermediateKid);
