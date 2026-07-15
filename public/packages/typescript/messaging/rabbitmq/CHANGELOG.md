<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Changelog — @dcsv-io/d2-messaging-rabbitmq

All notable changes to this package are documented here. Versions follow the
per-package semver + build-free-diff convention.

## [Unreleased]

### Wire-breaking

### API-breaking

### Added

- Consumer-side crypto: `CryptoBodyOpener` (sealed / symmetric) +
  `assertOpenerMatchesDomain`, and `BodyOpener.open` may now return a `Promise` —
  a decrypting WebCrypto opener is inherently async and the delivery pipeline
  awaits it; the default plaintext opener stays synchronous.
- The auto-encrypting publisher fusion — `createPublisher` / `publishVia` (+ the
  `D2Publisher`, `CreatePublisherOptions`, `PublishEnvelope`, `PublishSender`
  types), the `composeBody` composer, and the `DomainCryptoMap` mode-branded type
  witness (`ComposerFor`, `EncryptedDomain`, `PublishableKey`, `PublishableKeyOf`,
  `CatalogEncryption`) plus `readEncryptionKid`. An unwired encrypted
  domain is a COMPILE error, with a runtime default-deny second lock — retiring
  the prior consumer-only fence. The publisher's `message-id` is minted by
  `uuidv7`, now homed in `@dcsv-io/d2-utilities` (was briefly exported here).

### Fixed

## 0.1.0

Initial release: the service-agnostic RabbitMQ **consumer** runtime — the
TypeScript twin of the .NET `DcsvIo.D2.Messaging.RabbitMq` consumer path.

- `subscribe` / `createConnection` public surface (consumer-only; no publisher).
- Topology declaration matching the .NET contract exactly (primary + `{q}.dlx`
  + `{q}.dlq` + optional retry tiers), byte-identical `DlqNaming`.
- Manual-ack consume with the DLQ republish-with-`DlqFailureMetadata`-then-ack
  failure path (+ NACK-no-requeue fallback).
- The precise 5-point idempotency contract over an `IMessageIdempotencyStore`
  seam (+ in-memory 24h-TTL impl).
- Consume-side context establishment: traceparent-parented `Consumer`-kind span
  + `x-d2-context` (base64url-of-JSON) decoded and applied onto a fresh
  per-message context (identity + `RequestOrigin` never taken from the wire).
- Injectable body-decompose seam (plaintext now; encrypted body with no
  registered opener → fail-loud DLQ `DECRYPT_FAILURE`).
- Reconnect (rabbitmq-client auto-recovery + aux-topology re-declaration).
