// -----------------------------------------------------------------------
// <copyright file="CaCertificateFiles.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Infra.Vault.File;

/// <summary>
/// Filename constants for the file-backed dev certificate-authority chain. The
/// provider and the dev-key generation script pin the same literals through these
/// constants so a producer/consumer filename mismatch is impossible.
/// </summary>
public static class CaCertificateFiles
{
    /// <summary>The fixed filename of the root CA certificate (PEM) in the CA directory.</summary>
    public const string ROOT_CERT_FILE_NAME = "ca-root.crt";

    /// <summary>The fixed filename of the root CA private key (PKCS#8 PEM) in the CA directory.</summary>
    public const string ROOT_KEY_FILE_NAME = "ca-root.key";

    /// <summary>The fixed filename of the intermediate CA certificate (PEM) in the CA directory.</summary>
    public const string INTERMEDIATE_CERT_FILE_NAME = "ca-intermediate.crt";

    /// <summary>
    /// The fixed filename of the intermediate CA private key (PKCS#8 PEM) in the CA
    /// directory.
    /// </summary>
    public const string INTERMEDIATE_KEY_FILE_NAME = "ca-intermediate.key";
}
