// -----------------------------------------------------------------------
// <copyright file="KeyCustodianRootKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Infrastructure.Vault;

/// <summary>
/// Keyed-services discriminator for the KeyCustodian root keyring + root payload
/// crypto.
/// </summary>
public static class KeyCustodianRootKey
{
    /// <summary>
    /// The keyed-services discriminator under which the root keyring +
    /// <c>IPayloadCrypto</c> are registered. Handlers inject the root crypto via
    /// <c>[FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto</c>.
    /// </summary>
    public const string ROOT_SERVICE_KEY = "keycustodian-root";
}
