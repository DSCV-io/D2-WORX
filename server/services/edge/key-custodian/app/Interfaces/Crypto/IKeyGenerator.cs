// -----------------------------------------------------------------------
// <copyright file="IKeyGenerator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Interfaces.Crypto;

using D2.Edge.KeyCustodian.App.Crypto;
using D2.Edge.KeyCustodian.Domain.Enums;

/// <summary>
/// Strategy for generating fresh key material for one <see cref="KeyType"/>.
/// One implementation per key type; the command handler selects the strategy
/// whose <see cref="Handles"/> matches the requested type.
/// </summary>
public interface IKeyGenerator
{
    /// <summary>Gets the key type this generator produces material for.</summary>
    KeyType Handles { get; }

    /// <summary>
    /// Generates fresh key material. The returned <see cref="GeneratedKeyMaterial.Plaintext"/>
    /// is raw and unencrypted — the caller root-wraps it then zeroes it.
    /// </summary>
    /// <returns>The freshly-generated material.</returns>
    GeneratedKeyMaterial Generate();
}
