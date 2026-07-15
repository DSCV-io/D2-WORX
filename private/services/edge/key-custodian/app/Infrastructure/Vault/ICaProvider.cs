// -----------------------------------------------------------------------
// <copyright file="ICaProvider.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Vault;

/// <summary>
/// App-layer port that resolves the dev certificate-authority hierarchy (root +
/// issuing intermediate) used to seed the managed CA keys on startup.
/// </summary>
/// <remarks>
/// The concrete implementation (a file-backed provider reading the CA chain from
/// the root-key directory) lives in the Infra layer; the App layer depends only on
/// this port so the Domain + App stay free of file / secret access. This port is
/// the bootstrap/load seam — once seeded, every consumer (issuance, rotation,
/// compromise-replacement) reads the active CA from the database, not from this
/// provider.
/// </remarks>
public interface ICaProvider
{
    /// <summary>
    /// Loads, chain-validates, and returns the dev CA hierarchy used to seed the
    /// managed root + intermediate keys.
    /// </summary>
    /// <returns>
    /// <c>Ok(<see cref="LoadedCaMaterial"/>)</c> with both tiers' certificate +
    /// private-key material on success.
    /// A typed <c>ServiceUnavailable</c> failure (never a thrown exception) when the
    /// CA chain cannot be loaded — missing file, malformed PEM, wrong-curve key,
    /// intermediate not chaining to the root, or a certificate outside its validity
    /// window. Callers MUST check <c>BubbleOnFailure</c> and handle the failure
    /// path; they MUST NOT assume <c>Success == true</c> on every call.
    /// The returned <see cref="LoadedCaMaterial"/> is single-use: callers MUST call
    /// <see cref="LoadedCaMaterial.Zero"/> after wrapping the private keys.
    /// </returns>
    D2Result<LoadedCaMaterial> GetSeedCaMaterial();
}
