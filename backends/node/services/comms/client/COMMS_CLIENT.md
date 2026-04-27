# @d2/comms-client

Thin client library for publishing notification requests to the Comms service via RabbitMQ. Consuming services call a single `Notify` handler with a universal message shape; the Comms service resolves the recipient's address, picks channels, renders markdown, and delivers.

Mirrors the `@d2/geo-client` pattern: a service-owned client that lives alongside the service it fronts, registered into any consumer's DI container.

## File Tree

```
client/
├── package.json
├── tsconfig.json
├── COMMS_CLIENT.md
└── src/
    ├── index.ts                     # Public API re-exports
    ├── comms-client-constants.ts    # RabbitMQ exchange names
    ├── registration.ts              # DI registration (addCommsClient)
    ├── service-keys.ts              # DI ServiceKeys (INotifyKey → INotifyHandler)
    ├── interfaces/
    │   ├── index.ts
    │   └── pub/
    │       ├── index.ts
    │       └── notify.ts            # INotifyHandler interface
    └── handlers/
        └── pub/
            └── notify.ts            # Notify handler (implements INotifyHandler)
```

---

## Universal Message Shape

Every notification published through the client uses a single `NotifyInput` shape. The Comms service decides which channels to use based on the caller-supplied `channels` array, the `urgency` flag, and the recipient's stored channel preferences.

| Field                | Type                      | Required | Description                                                                                                                                                                               |
| -------------------- | ------------------------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `recipientContactId` | `string` (UUID)           | Yes      | Geo contact ID -- the ONLY recipient identifier                                                                                                                                           |
| `title`              | `string` (max 255)        | Yes      | Email subject, SMS prefix, push title                                                                                                                                                     |
| `content`            | `string` (max 50,000)     | Yes      | Markdown body -- rendered to HTML for email                                                                                                                                               |
| `plaintext`          | `string` (max 50,000)     | Yes      | Plain text -- SMS body, email fallback                                                                                                                                                    |
| `channels`           | `Channel[]`               | No       | Explicit caller override -- attempt exactly these channels. Empty/omitted = fall back to the recipient's stored channel preferences. `Channel = "email" \| "sms"` from `@d2/comms-domain` |
| `urgency`            | `"normal"` \| `"urgent"`  | No       | Default `"normal"`. `"urgent"` forces ALL channels (email + sms), overriding both `channels` and recipient preferences                                                                    |
| `correlationId`      | `string` (max 36)         | Yes      | Idempotency key for deduplication                                                                                                                                                         |
| `senderService`      | `string` (max 50)         | Yes      | Source service identifier (e.g. `"auth"`, `"billing"`)                                                                                                                                    |
| `metadata`           | `Record<string, unknown>` | No       | Arbitrary key-value pairs for future use                                                                                                                                                  |

All fields are validated via Zod before publishing.

### Channel Resolution Precedence

The Comms service applies the `resolveChannels` rule (in `@d2/comms-domain/rules/channel-resolution.ts`):

1. **`urgency === "urgent"`** -- force ALL channels (`email` + `sms`); ignore `channels` and recipient preferences
2. **`channels` non-empty** -- caller override: use exactly these channels; recipient preferences are ignored
3. **`channels` empty/undefined** -- fall back to recipient preferences (`emailEnabled`, `smsEnabled`)

Channel preferences are **opt-out**: when a recipient has no `channel_preference` row, both `emailEnabled` and `smsEnabled` default to `true`.

Use `channels: ["email"]` for tokens / PII / anything that must not leak via SMS. Leave `channels` unset to honour the recipient's stored preferences.

---

## Registration

Register into any service's DI container during composition:

```ts
import { addCommsClient } from "@d2/comms-client";

addCommsClient(services, { publisher: messageBus.publisher });
```

`addCommsClient` accepts an `AddCommsClientOptions` with a single optional field:

| Option      | Type                             | Description                                                                        |
| ----------- | -------------------------------- | ---------------------------------------------------------------------------------- |
| `publisher` | `IMessagePublisher \| undefined` | RabbitMQ publisher. Omit for local dev / tests -- handler logs and returns success |

The handler is registered as **transient** under `INotifyKey`.

---

## Usage

Resolve `INotifyKey` from the DI scope and call `handleAsync`:

```ts
import { INotifyKey } from "@d2/comms-client";

const notify = scope.resolve(INotifyKey);

const result = await notify.handleAsync({
  recipientContactId: "01926a3b-...",
  title: "Verify your email",
  content: "Click [here](https://...) to verify.",
  plaintext: "Visit https://... to verify your email.",
  correlationId: crypto.randomUUID(),
  senderService: "auth",
});
```

### Auth Service Example (typical caller)

Auth resolves the user's Geo contact ID before notifying:

```ts
// 1. Resolve contact via geo-client (ext-key lookup)
const contacts = await getContactsByExtKeys.handleAsync({
  contextKey: "auth_user",
  relatedEntityId: userId,
});

// 2. Publish notification (email-only -- contains a reset token)
const notify = scope.resolve(INotifyKey);
await notify.handleAsync({
  recipientContactId: contacts.data![0].id,
  title: "Password reset",
  content: "Use this [link](https://...) to reset your password.",
  plaintext: "Visit https://... to reset your password.",
  correlationId: crypto.randomUUID(),
  senderService: "auth",
  channels: ["email"],
});
```

---

## Key Design Decisions

| Decision                           | Rationale                                                                                                                                                                                |
| ---------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Contacts only, never userIds**   | Decouples identity from delivery. Comms resolves addresses from Geo contacts, never queries Auth                                                                                         |
| **Explicit `channels[]` override** | Caller supplies exactly which channels to attempt (e.g., `["email"]` for tokens / PII). Replaces the older opaque `sensitive: boolean` flag with something the caller controls precisely |
| **Empty `channels` -> use prefs**  | Omitting `channels` (or passing `[]`) falls through to the recipient's stored `channel_preference` row. Defaults are opt-out (both channels enabled) when no preference row exists       |
| **`urgency` overrides everything** | `"urgent"` forces delivery on ALL channels regardless of `channels` or recipient notification preferences                                                                                |
| **Fire-and-forget via RabbitMQ**   | Publisher returns success once the message is enqueued. Comms handles retries, rendering, and delivery                                                                                   |
| **No-op without publisher**        | When `publisher` is omitted, handler logs the notification and returns `Ok` -- safe for tests and local dev                                                                              |
| **Single exchange, no routing**    | All notifications go to `comms.notifications` fanout exchange with empty routing key                                                                                                     |

---

## Messaging Topology

| Constant                 | Value                 | Type            | Used By                               |
| ------------------------ | --------------------- | --------------- | ------------------------------------- |
| `NOTIFICATIONS_EXCHANGE` | `comms.notifications` | Fanout exchange | comms-client (pub), comms-infra (sub) |

Shared via `COMMS_EVENTS` constant object, imported by both the client publisher and the Comms service consumer to keep exchange naming in sync.

---

## Dependencies

| Package         | Purpose                                |
| --------------- | -------------------------------------- |
| `@d2/di`        | ServiceKey + ServiceCollection for DI  |
| `@d2/handler`   | BaseHandler + IHandlerContext          |
| `@d2/messaging` | IMessagePublisher for RabbitMQ publish |
| `@d2/result`    | D2Result return type                   |
| `zod`           | Input validation schema                |

---

## .NET Equivalent

No .NET `Comms.Client` exists yet. When built, it would follow the same pattern as `Geo.Client`: a class library with handler interfaces, DI extensions, and RabbitMQ publishing -- consumed by the .NET Gateway or any .NET service needing to send notifications.
