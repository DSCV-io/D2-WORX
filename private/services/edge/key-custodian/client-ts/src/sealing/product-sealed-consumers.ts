// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Product sealed-domain consumer ServiceIds from
 * `private/contracts/encryption-domains/encryption-domains.spec.json`.
 *
 * Public `@d2/encryption-abstractions` only ships framework domains
 * (plaintext + payload-fixture-sealed). The KeyCustodian client is a
 * product surface and must seal to product consumers (audit /
 * notifications / courier) as well as any public sealed fixture.
 *
 * Keep in lockstep with the private encryption-domains values file
 * (and .NET `ProductEncryptionDomainModes.ConsumerServiceByDomain`).
 */
export const PRODUCT_SEALED_CONSUMER_SERVICES = [
  "audit",
  "notifications",
  "courier",
] as const;
