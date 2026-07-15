// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Runtime crypto twin of .NET `D2.Shared.Encryption` — WebCrypto AES-256-GCM
// (v1 symmetric) + P-256 ECDH-ES → HKDF-SHA256 → AES-256-GCM (v2 sealed), both
// directions, byte-identical to the .NET encoder (KAT-pinned). Consumes the
// wire-layout constants from @d2/encryption-abstractions.

// Ports.
export type {
  IPayloadCrypto,
  IPayloadSealer,
  IPayloadOpener,
} from "./ports.js";

// Typed failure taxonomy (twins of the .NET exception hierarchy).
export {
  EncryptionError,
  FrameMalformedError,
  FrameVersionMismatchError,
  KidNotInKeyringError,
  AuthenticationTagMismatchError,
} from "./errors.js";

// Symmetric (version-1) keyring + crypto.
export { PayloadCryptoKeyring } from "./payload-crypto-keyring.js";
export { PayloadCrypto } from "./payload-crypto.js";

// Sealed (version-2) keyrings + sealer/opener.
export { RecipientPublicKeyring } from "./recipient-public-keyring.js";
export { RecipientPrivateKeyring } from "./recipient-private-keyring.js";
export { PayloadSealer } from "./payload-sealer.js";
export { PayloadOpener } from "./payload-opener.js";
