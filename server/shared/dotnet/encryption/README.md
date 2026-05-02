<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Encryption

> **Status**: placeholder — not yet implemented.

## Purpose

AES-256-GCM payload encryption with **JWKS-style multi-key keyring** for graceful overlap rotation. Consumed transparently by `D2.Shared.Messaging` (via the `[Encrypted(Domain.X)]` attribute) — call sites stay clean.

## Public API surface

- `PayloadCryptoKeyring` — holds N keys indexed by `kid`, with one designated active for encryption
  - `ActiveKey { get; }`
  - `TryGetKey(string kid, out ReadOnlyMemory<byte> key)`
  - `AllKids { get; }` (for ops / diagnostics)
- `IPayloadCrypto` — `Encrypt(ReadOnlySpan<byte>) → byte[]` (uses active kid) + `Decrypt(ReadOnlySpan<byte>) → byte[]` (reads kid from frame, looks up)
- `PayloadCrypto(PayloadCryptoKeyring)` — concrete impl
- `[Encrypted(Domain.X)]` attribute marker (consumed by `D2.Shared.Messaging`)
- Frame format (self-contained — kid travels with ciphertext): `[1B version][1B kid_length][N bytes kid][12B GCM nonce][M bytes ciphertext+tag]`
- DI registration: `services.AddD2Encryption(domain, keyringSource)` — keyring source is typically `KeyringClient` from `D2.Shared.Auth`

## Dependencies

- `D2.Shared.Result` (decrypt failures)
- `D2.Shared.Utilities` (logging, key disposal)
- `System.Security.Cryptography` (AesGcm — .NET native)

## References

- Payload encryption (D2.Shared.Encryption) — full design
- KeyCustodian — manages key lifecycle; this lib consumes keys via `KeyringClient`
- [docs/SECURITY-RUNBOOKS.md](../../../../docs/SECURITY-RUNBOOKS.md) — compromise response runbooks
