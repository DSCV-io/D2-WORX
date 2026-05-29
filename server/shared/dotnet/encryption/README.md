<!--
Copyright (c) DCSV. All rights reserved.
-->

# encryption/

> Parent: [`server/shared/dotnet/`](../README.md)

Payload-encryption primitives for service-to-service confidentiality — consumed wherever a sensitive payload crosses a trust boundary (notably the async messaging path). The cluster holds the AES-256-GCM crypto core (keyring, frame format, keyed-services DI) plus the spec-driven source generators that emit the closed keyring-domain catalog, the binary frame-layout offsets, and the in-process context-slot keys from `contracts/`. The crypto core knows nothing about domains or message buses; the domain and key concerns are codegen-emitted constants.

## Packages

| Package                                                       | Description                                                                                                                                       |
| ------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| [`core/`](core/README.md)                                     | The crypto primitive — `PayloadCryptoKeyring`, `IPayloadCrypto` + `PayloadCrypto` (AES-256-GCM), self-describing frame format, keyed-services DI. |
| [`domains-source-gen/`](domains-source-gen/README.md)         | Roslyn generator emitting the closed keyring-domain catalog (`EncryptionDomains`) into `core/` from `contracts/encryption-domains/encryption-domains.spec.json`. |
| [`frame-source-gen/`](frame-source-gen/README.md)             | Roslyn generator emitting the binary frame-layout field-offset / byte-length constants into `core/` from `contracts/encryption-frame/encryption-frame.spec.json`. |
| [`in-process-keys-source-gen/`](in-process-keys-source-gen/README.md) | Roslyn generator emitting the in-process context-slot keys (`D2HttpContextItems` / `D2GrpcUserStateKeys`) into `auth/abstractions/` and `auth/grpc/` from `contracts/in-process-keys/keys.spec.json`. |
