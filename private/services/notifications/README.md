<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Notifications

> Parent: [`private/services/`](../README.md)

> **Status**: NOT IMPLEMENTED — not yet built.

## Purpose

In-app activity feed. Persistent feed entries with read/unread state, pagination, aggregation ("Alice and 3 others liked your post").

The persistent feed is the source of truth for in-app notifications — WS push is an optimization for live delivery. Without persistence, entity-notification reliability collapses to "WS-only delivery, lost if offline."

## Public API surface

- REST API:
  - `GET /api/v1/notifications` — paginated feed
  - `PATCH /api/v1/notifications/{id}/read` — mark read
  - `PATCH /api/v1/notifications/read-all` — mark all read
- RabbitMQ consumer: `d2.notifications.requests` exchange (encrypted) — producers publish `NotificationRequestedEvent`
- Outbound: gRPC to Edge SignalR push API (`notification.created` push to recipient's connections)

## Dependencies (.NET shared libs)

- `DcsvIo.D2.Messaging` (consumer for `d2.notifications.requests` + publisher to `d2.audit.events`)
- `DcsvIo.D2.Encryption` (decrypts RMQ payloads)
- `DcsvIo.D2.Auth` (recipient identity validation, scope checks on REST endpoints)
- `DcsvIo.D2.I18n` (notification subject + body rendering with locale)
- `DcsvIo.D2.Contacts` (recipient resolution via `notifications_contacts_db`)

## Database

- `notifications_db` — owned by D2.Notifications. Schema: `notification_feed_entry` (id, recipient_user_id, producer_service, event_type, subject markdown, body markdown, link, metadata JSONB, urgency, aggregation_key nullable, read_at, created_at). Indexed on `(recipient_user_id, read_at, created_at)`.
- `notifications_contacts_db` — via `DcsvIo.D2.Contacts` library.

## Aggregation

Aggregation rules live in Notifications (it owns the feed shape). Producers don't decide whether their event is aggregated — they emit the event with an `aggregation_key`; Notifications decides how to roll up.

## What Notifications does NOT do

- **Does NOT decide whether to also send email/SMS** — that's the producer's call. Producers publish to BOTH `d2.notifications.requests` (for in-app) AND call D2.Courier (for email/SMS) explicitly.
- **Not a delivery service** — D2.Courier handles outbound delivery. Notifications handles in-app feed only.

## Client library

`private/services/notifications/clients/dotnet/D2.Notifications.Client.csproj` — thin RabbitMQ publisher for `NotificationRequestedEvent`. Shipped alongside the service.

## References

- [`public/packages/dotnet/messaging/rabbitmq/README.md`](../../shared/dotnet/messaging/rabbitmq/README.md) — RabbitMQ patterns + at-least-once delivery semantics
- [docs/PATTERNS.md](../../../docs/PATTERNS.md) — handler / D2Result / RedactionSpec patterns
