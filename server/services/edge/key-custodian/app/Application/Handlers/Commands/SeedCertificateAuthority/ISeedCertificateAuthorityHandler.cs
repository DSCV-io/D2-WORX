// -----------------------------------------------------------------------
// <copyright file="ISeedCertificateAuthorityHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority;

/// <summary>
/// Seeds the certificate-authority hierarchy on startup: loads the root +
/// intermediate from the <c>ICaProvider</c> and persists them as active managed
/// keys. Idempotent — a second call on an already-seeded store is a no-op.
/// </summary>
public interface ISeedCertificateAuthorityHandler
    : IHandler<SeedCertificateAuthorityInput, SeedCertificateAuthorityOutput>;
