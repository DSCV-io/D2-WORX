// -----------------------------------------------------------------------
// <copyright file="RootKeyKids.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Infra.Vault.File;

/// <summary>
/// Kid + filename constants for the file-backed root keyring. The provider and
/// its tests pin the same literals through these constants so a primary/successor
/// mismatch is impossible.
/// </summary>
public static class RootKeyKids
{
    /// <summary>
    /// The kid of the PRIMARY root key — the <see cref="PayloadCryptoKeyring.ActiveKid"/>
    /// used to wrap all new key material.
    /// </summary>
    public const string PRIMARY_KID = "root";

    /// <summary>
    /// The kid of the OPTIONAL successor root key — a decrypt-only kid present
    /// only during a root-rotation window. Absent in steady state.
    /// </summary>
    public const string NEXT_KID = "root-next";

    /// <summary>The fixed filename of the primary root key inside the root directory.</summary>
    public const string PRIMARY_FILE_NAME = "root.key";

    /// <summary>The fixed filename of the successor root key inside the root directory.</summary>
    public const string NEXT_FILE_NAME = "root-next.key";
}
