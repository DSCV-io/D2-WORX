// -----------------------------------------------------------------------
// <copyright file="SeedCertificateAuthorityInput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority;

/// <summary>
/// Input to <c>SeedCertificateAuthority</c>. The hierarchy to seed is read from
/// the <c>ICaProvider</c>, so this command carries no parameters.
/// </summary>
public sealed record SeedCertificateAuthorityInput;
