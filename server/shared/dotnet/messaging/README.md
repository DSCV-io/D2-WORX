<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Messaging

> **Status**: placeholder — not yet implemented.

## Purpose

Thin RabbitMQ wrapper — proto-canonical-JSON serialization, `[Encrypted(Domain.X)]` attribute integration with `D2.Shared.Encryption`, AMQP headers contract, exchange / queue declarations. Publishers + consumers use this; nobody talks to RabbitMQ directly.

## Public API surface

- `IMessageBus` — `Publish<T>(exchange, routingKey, message)` + `Subscribe<T>(queue, handler)`
- Subscription handlers — `IMessageHandler<T>` interface; per-message-type handlers register into the bus
- `[Encrypted(Domain.X)]` attribute on message types — bus auto-encrypts on publish + decrypts on subscribe via `D2.Shared.Encryption.IPayloadCrypto`
- AMQP headers: auto-populated `content-type`, `x-proto-type`, `message-id`, `timestamp`, `x-d2-trace-id`, `x-d2-correlation-id` (per [docs/MESSAGING.md](../../../../docs/MESSAGING.md))
- Queue declarations: durable shared (default), exclusive auto-delete (for fanout cache-invalidation)
- DI registration: `services.AddD2Messaging(rmqConnection)`

## Dependencies

- `D2.Shared.Handler`
- `D2.Shared.Result`
- `D2.Shared.Encryption` (for `[Encrypted]` attribute support)
- `D2.Shared.Utilities` (logging, retries)
- `RabbitMQ.Client`
- `Google.Protobuf` (proto-canonical JSON)

## References

- [docs/MESSAGING.md](../../../../docs/MESSAGING.md) — full wire format, AMQP headers, exchange / routing-key naming, encryption frame, queue topology, DLQ behavior
- [`../encryption/README.md`](../encryption/README.md) — `[Encrypted(Domain.X)]` integration mechanics + frame format
- [docs/OPERATIONAL-GUARANTEES.md](../../../../docs/OPERATIONAL-GUARANTEES.md) — at-least-once delivery, idempotency contract for consumers
