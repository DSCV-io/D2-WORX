<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.EncryptionDomains.SourceGen

Roslyn incremental source generator that emits the `EncryptionDomains` static class — the closed catalog of encryption-domain identifiers — from `contracts/encryption-domains/encryption-domains.spec.json`.

## What this emits

When the consuming assembly is `D2.Shared.Encryption`, the generator emits `EncryptionDomains.g.cs` containing one `public const string` per spec entry plus an `AllDomains` membership list.

The closed catalog includes the `PLAINTEXT` sentinel so the `MqMessages.SourceGen` cross-validation of `mq-messages.spec.json:encryption` field values has one unambiguous source of truth.

## Why spec-drive this

A typo on either the producer or consumer side surfaces as a compile error rather than silently routing a message to a non-existent keyring at runtime. The spec is the closed catalog of valid identifiers, enforced at codegen time.

## Cross-language parity

The SAME spec drives `@d2/encryption-abstractions` via `tools/ts-codegen/src/encryption-domains-emit.ts`. Any TS code reading the catalog (ops tooling, RabbitMQ subscribers, encryption pipelines) shares byte-equal identifiers with the .NET producers.

## Diagnostics

| ID | Title | Severity |
|---|---|---|
| `D2ED001` | Encryption domains spec is malformed | Error |
| `D2ED002` | Duplicate constName | Error |
| `D2ED003` | Duplicate wire value | Error |
| `D2ED004` | constName has invalid shape | Error |
| `D2ED005` | Empty wire value | Error |
