<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_5_REFERENCE.md — D2.Courier + D2.Notifications

> **Phase 5** per V2.md §4. Reference doc preserved during rebuild — **delete this file after Phase 5 ships**.
>
> **Source**: distilled from v1 `backends/node/services/comms/COMMS.md` + `COMMS_CLIENT.md` (preserved in `/old/v1/D2-WORX/`).

---

## D2.Courier + D2.Notifications Split

v1 had a single `comms` service that lumped outbound delivery + in-app feed + future conversational messaging. v2 splits per V2.md §5.7:

- **D2.Courier** — pure outbound delivery (email + SMS + future webhooks/Slack/Teams/push). The "send" side.
- **D2.Notifications** — in-app activity feed (persistent feed entries, read/unread state, pagination, aggregation). The "show in the UI" side.
- **D2.Threads** (deferred — Phase 9 / future) — conversational messaging.

Both build in Phase 5 (per V2.md §4 — promoted from "future" because the WS-as-optimization model needs a persistent source of truth).

---

## D2.Courier — 6 Design Principles (preserved from v1 COMMS.md)

1. **Senders send content; the delivery service handles presentation.** Producers provide markdown subject + markdown body + variables; D2.Courier renders to HTML, applies brand chrome, picks the right channel.
2. **Fire-and-forget at the producer.** Producers publish to RabbitMQ and move on. D2.Courier handles retries, channel selection, delivery status.
3. **Async feedback via SignalR.** Delivery status (delivered, bounced, opened) flows back via SignalR push, not synchronous response.
4. **Channel-agnostic API.** Producers don't pick "email" vs "SMS" vs "in-app". They emit a delivery request with content + recipient context; D2.Courier resolves the channel based on user preferences.
5. **Contacts-only recipient resolution.** Producers identify recipients by contact ID (or user ID + context), not by raw email/phone. D2.Courier resolves to the actual destination.
6. **RabbitMQ as queue + PostgreSQL as audit.** Every delivery request gets a unique `correlationId` (unique index in PG) for cross-service tracking. Delivery attempts logged in PG with `(request_id, channel, attempt_number)` uniqueness.

Phase 5 implementations should embody all 6.

---

## Universal Message Shape (preserved from v1 COMMS_CLIENT.md)

The contract producers use to send notifications. Phase 5 D2.Courier client must preserve this shape (or document deliberate divergences).

| Field | Type | Required? | Purpose |
|---|---|---|---|
| `title` | string (markdown) | Yes | Email subject / SMS subject prefix / notification title |
| `content` | string (markdown) | Yes | Email body / SMS body / notification body — markdown rendered to HTML for email, plaintext-stripped for SMS |
| `plaintext` | string | No (auto-derived if absent) | Plain-text fallback for email; auto-generated from `content` via Markdig PlainTextRenderer if not provided |
| `channels` | array of channel hints (`email`, `sms`, `in_app`) | No | Producer's preferred channel(s); D2.Courier may override based on user preferences + availability |
| `urgency` | enum (`low`, `normal`, `high`, `critical`) | Yes | Drives retry behavior + channel escalation policy |
| `correlationId` | UUID | Yes | Unique per delivery request — for cross-service tracking + dedup |
| `senderService` | string | Yes | Producer service name (`d2-files`, `d2-edge`, etc.) — for audit + observability |
| `metadata` | JSON object | No | Free-form metadata (e.g., `{eventType: "file_processed"}`) — surfaces in audit + analytics |

**Variable substitution**: producers can include `{placeholder}` tokens in `title` + `content` + `plaintext` and pass a `variables` map. D2.Courier substitutes (HTML-escaped on insertion; no escape hatch — sender-supplied content is NEVER trusted to be safe HTML).

---

## D2.Courier internals (per V2.md §5.7)

- ❌ No contact storage, no contact resolution (contacts library handles this)
- ❌ No template entity, no template management (sender provides content)
- ❌ No conversational messaging (that's Threads)
- ❌ No in-app feed (that's Notifications)
- ✅ Receives delivery request: subject (markdown w/ placeholders), body (markdown w/ placeholders), variables map, channel hints, recipient delivery info, prefs scoping context
- ✅ Substitutes variables (HTML-escaped on insertion)
- ✅ Email rendering: markdown → HTML via `Markdig` (`UseAdvancedExtensions().DisableHtml()`), HTML sanitiser via `Ganss.Xss`, brand chrome via Razor template (branding vars only — colour, logo image)
- ✅ SMS: substitute variables into plain-text body, no rendering, forward via Twilio
- ✅ Plain-text fallback for email derived automatically via Markdig's `PlainTextRenderer`
- ✅ Lookup delivery preferences scoped by `(UserId/OrgId) × (RelatedServiceName/Key/Id)`; most-specific match wins
- ✅ Deliver via SMTP / Twilio
- ✅ Retry via tier-queue topology
- ✅ Track delivery status

---

## D2.Notifications internals (per V2.md §5.7)

- Persistent feed entries with read/unread state, pagination, aggregation ("Alice and 3 others liked your post")
- Schema: `notification_feed_entry` — `id`, `recipient_user_id`, `producer_service`, `event_type`, `subject` (markdown), `body` (markdown), `link` (deep-link URL), `metadata` (JSONB), `urgency`, `aggregation_key` (nullable), `read_at`, `created_at`. Indexed on `(recipient_user_id, read_at, created_at)`.
- Pipeline:
  - Producers publish `NotificationRequestedEvent` to `d2.notifications.requests` exchange (decouples producers from Notifications availability)
  - Notifications consumes, persists feed entry, calls Edge's SignalR push API (gRPC) to push a `notification.created` SignalR message to recipient's connections
  - REST API: `GET /api/v1/notifications`, `PATCH /api/v1/notifications/{id}/read`, `PATCH /api/v1/notifications/read-all`
  - Aggregation rules live in Notifications (it owns the feed shape)
- Notifications does **not** decide whether to also send email/SMS — that's the producer's call (publish to `d2.notifications.requests` AND call D2.Courier explicitly)

---

## When This Doc Gets Deleted

Phase 5 completion criteria includes:
- [ ] D2.Courier client library (`server/services/courier/clients/dotnet/`) ships with the Universal Message Shape preserved
- [ ] D2.Notifications service ships with the schema + pipeline above
- [ ] D2.Courier service ships embodying the 6 design principles
- [ ] Per-service READMEs (`server/services/courier/README.md`, `server/services/notifications/README.md`) capture the Phase 5 details
- [ ] V2.md §5.7 is the canonical reference for both services going forward

Once the per-service READMEs exist + are accurate, this reference doc has served its purpose. Move to `docs/archive/PHASE_5_REFERENCE.md` or delete (reference is in git history).
