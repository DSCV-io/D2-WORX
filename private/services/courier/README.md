<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Courier

> Parent: [`private/services/`](../README.md)

> **Status**: NOT IMPLEMENTED — not yet built.

## Purpose

Pure outbound delivery — email + SMS. The "send" side. Markdown content rendered to HTML via Markdig; brand chrome via Razor.

D2.Notifications is the "show in the UI" side. Each owns one slice — Courier never persists feed state; Notifications never sends emails.

## Six design principles

1. Senders send content; the delivery service handles presentation
2. Fire-and-forget at the producer
3. Async feedback via SignalR
4. Channel-agnostic API
5. Contacts-only recipient resolution
6. RabbitMQ as queue + PostgreSQL as audit

## Public API surface

- gRPC: `Notify(NotificationRequest)` — universal message shape (`title` / `content` / `plaintext` / `channels` / `urgency` / `correlationId` / `senderService` / `metadata`)
- RabbitMQ consumer: `d2.courier.deliver` exchange (encrypted)
- REST API: delivery status query (consumer-facing)
- Outbound: SMTP (or Resend HTTP), Twilio HTTP

## Dependencies (.NET shared libs)

- `DcsvIo.D2.Messaging` (consumer + publisher to `d2.audit.events` for cross-cutting audit)
- `DcsvIo.D2.Encryption` (decrypts RMQ payloads from `d2.courier.deliver`)
- `DcsvIo.D2.Contacts` (recipient resolution via `courier_contacts_db`)
- `DcsvIo.D2.Geo`, `DcsvIo.D2.Location` (locale + currency for templated content)
- `DcsvIo.D2.I18n` (template rendering with locale-aware variables)

## Database

- `courier_db` — owned by D2.Courier. Schema: `delivery_request` (correlationId unique index), `delivery_attempt` (`(request_id, channel, attempt_number)` unique), `delivery_status_history`, channel preference rows, brand chrome configs.
- `courier_contacts_db` — via `DcsvIo.D2.Contacts` library.

## Client library

`private/services/courier/clients/dotnet/D2.Courier.Client.csproj` — thin RabbitMQ publisher per the Universal Message Shape. Shipped alongside the service so consumers can publish without rolling their own AMQP code.

## References

- [`public/packages/dotnet/messaging/rabbitmq/README.md`](../../shared/dotnet/messaging/rabbitmq/README.md) — RabbitMQ wire format, exchange naming, encryption, delivery semantics
- [docs/PATTERNS.md](../../../docs/PATTERNS.md) — handler / D2Result / RedactionSpec patterns
