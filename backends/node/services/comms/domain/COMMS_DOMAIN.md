# @d2/comms-domain

Pure domain types for the D2-WORX Comms service. Zero infrastructure dependencies -- only depends on `@d2/utilities` for string cleaning, email validation, and UUIDv7 generation.

## Purpose

Defines entities, enums, business rules, and constants for both the **delivery engine** (transactional email/SMS) and the **conversational messaging** system (threads, participants, reactions). These types are consumed by:

- `@d2/comms-app` (CQRS handlers)
- `@d2/comms-infra` (Drizzle repository, providers)
- `@d2/comms-api` (gRPC service, mappers)

## Design Decisions

| Decision                            | Rationale                                                                          |
| ----------------------------------- | ---------------------------------------------------------------------------------- | ------------------------------------------------------ |
| Readonly interfaces + factories     | More idiomatic TS than classes. Better tree-shaking, consistent functional style   |
| Immutable-by-default                | "Mutation" returns new objects via spread+override                                 |
| String literal unions (not enums)   | `as const` arrays + derived types. No TS `enum` keyword                            |
| `?: T` for optional data fields     | Optional entity fields use `?: T` (not `                                           | null`). Factories return `undefined` for absent values |
| contactId-only recipients           | Decouples identity from delivery. Comms resolves addresses via geo-client          |
| 2 urgency levels (normal, urgent)   | Simple model -- urgent bypasses preferences, normal respects them                  |
| No quiet hours                      | Deferred to a future phase                                                         |
| No template wrappers in domain      | Email template rendering is an app-layer concern (Deliver handler)                 |
| State machine transitions via rules | `transitionDeliveryAttemptStatus` and `transitionThreadState` enforce valid states |

## Package Structure

```
src/
  index.ts                  Barrel exports
  constants/
    comms-constants.ts      RETRY_POLICY, DELIVERY_DEFAULTS, CHANNEL_DEFAULTS, THREAD_CONSTRAINTS,
                            COMMS_MESSAGING, COMMS_RETRY
  enums/
    channel.ts              Channel: email | sms
    urgency.ts              Urgency: normal | urgent
    delivery-status.ts      DeliveryStatus: pending | sent | failed | retried (+ state machine)
    content-format.ts       ContentFormat: markdown | plain | html
    notification-policy.ts  NotificationPolicy: all_messages | mentions_only | none
    thread-type.ts          ThreadType: chat | support | forum | system
    thread-state.ts         ThreadState: active | archived | closed (+ state machine)
    participant-role.ts     ParticipantRole: observer | participant | moderator | creator (+ hierarchy)
  exceptions/
    comms-domain-error.ts   Base error (extends Error)
    comms-validation-error.ts  Structured validation error (entityName, propertyName, invalidValue, reason)
  entities/
    message.ts              Message interface + createMessage + editMessage + softDeleteMessage
    delivery-request.ts     DeliveryRequest interface + createDeliveryRequest + markDeliveryRequestProcessed
    delivery-attempt.ts     DeliveryAttempt interface + createDeliveryAttempt + transitionDeliveryAttemptStatus
    channel-preference.ts   ChannelPreference interface + createChannelPreference + updateChannelPreference
    message-receipt.ts      MessageReceipt interface + createMessageReceipt (per-user read tracking)
    thread.ts               Thread interface + createThread + updateThread + transitionThreadState
    thread-participant.ts   ThreadParticipant interface + create + update + markParticipantLeft
    message-attachment.ts   MessageAttachment interface + createMessageAttachment (immutable)
    message-reaction.ts     MessageReaction interface + createMessageReaction (immutable)
  value-objects/
    resolved-channels.ts    ResolvedChannels (channels + skippedChannels from channel resolution)
  rules/
    channel-resolution.ts   resolveChannels (sensitive/urgency/prefs -> channels)
    retry-policy.ts         computeRetryDelay, isMaxAttemptsReached, computeNextRetryAt
    thread-permissions.ts   canPostMessage, canEditMessage, canDeleteMessage, canManageParticipants,
                            canManageThread, canAddReaction
```

## Enums

All "enums" are `as const` arrays with derived union types and type guard functions.

| Enum               | Values                                    | Extras                                      |
| ------------------ | ----------------------------------------- | ------------------------------------------- |
| Channel            | email, sms                                | `isValidChannel` guard                      |
| Urgency            | normal, urgent                            | `isValidUrgency` guard                      |
| DeliveryStatus     | pending, sent, failed, retried            | `DELIVERY_STATUS_TRANSITIONS` state machine |
| ContentFormat      | markdown, plain, html                     | `isValidContentFormat` guard                |
| NotificationPolicy | all_messages, mentions_only, none         | `isValidNotificationPolicy` guard           |
| ThreadType         | chat, support, forum, system              | `isValidThreadType` guard                   |
| ThreadState        | active, archived, closed                  | `THREAD_STATE_TRANSITIONS` state machine    |
| ParticipantRole    | observer, participant, moderator, creator | `PARTICIPANT_ROLE_HIERARCHY` numeric map    |

## Entities

### Delivery Engine

| Entity            | Factory                   | Update / Transition                | Key Rules                                                                                                                |
| ----------------- | ------------------------- | ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| Message           | `createMessage`           | `editMessage`, `softDeleteMessage` | Content + plaintext required, at least one sender, UUIDv7 ID. 11 optional fields are `?: T`                              |
| DeliveryRequest   | `createDeliveryRequest`   | `markDeliveryRequestProcessed`     | Immutable except processedAt, correlationId for idempotency                                                              |
| DeliveryAttempt   | `createDeliveryAttempt`   | `transitionDeliveryAttemptStatus`  | Starts as "pending", state machine enforced, tracks attempt number. `providerMessageId`/`error`/`nextRetryAt` are `?: T` |
| ChannelPreference | `createChannelPreference` | `updateChannelPreference`          | Per-contact, defaults: email+sms enabled                                                                                 |

### Conversational Messaging

| Entity            | Factory                   | Update / Transition                              | Key Rules                                                                                                          |
| ----------------- | ------------------------- | ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------ |
| Thread            | `createThread`            | `updateThread`, `transitionThreadState`          | Starts as "active", slug accepted for any type (convention: forum only), state machine. Optional fields are `?: T` |
| ThreadParticipant | `createThreadParticipant` | `updateThreadParticipant`, `markParticipantLeft` | userId OR contactId required, role hierarchy enforced. Optional fields are `?: T`                                  |
| MessageReceipt    | `createMessageReceipt`    | --                                               | Immutable, UNIQUE(messageId, userId) at DB level                                                                   |
| MessageAttachment | `createMessageAttachment` | --                                               | Immutable, max 50 MB, file metadata only (storage via media svc)                                                   |
| MessageReaction   | `createMessageReaction`   | --                                               | Immutable, max 64 chars                                                                                            |

## Business Rules

| Rule                 | Function                                                                       |
| -------------------- | ------------------------------------------------------------------------------ | ------------------------------------------------------------- |
| Channel resolution   | `resolveChannels(prefs, message)` -- `prefs` parameter is `ChannelPreference   | undefined`. sensitive=email only, urgent=all, normal=by prefs |
| Retry delay          | `computeRetryDelay(attempt)` -- 5s, 10s, 30s, 60s, 300s (capped at last value) |
| Max attempts check   | `isMaxAttemptsReached(attempt)` -- true at 10 attempts                         |
| Next retry timestamp | `computeNextRetryAt(attempt, now)` -- current time + computed delay            |
| Post permission      | `canPostMessage(participant)` -- active + participant role or higher           |
| Edit permission      | `canEditMessage(participant, message)` -- own=participant+, others=moderator+  |
| Delete permission    | `canDeleteMessage(participant, message)` -- same rules as edit                 |
| Manage participants  | `canManageParticipants(participant)` -- active + moderator role or higher      |
| Manage thread        | `canManageThread(participant)` -- active + moderator role or higher            |
| Add reaction         | `canAddReaction(participant)` -- active + participant role or higher           |

## Constants

### RETRY_POLICY

| Constant       | Value                               | Purpose                                            |
| -------------- | ----------------------------------- | -------------------------------------------------- |
| `MAX_ATTEMPTS` | 10                                  | Maximum delivery attempts before permanent failure |
| `DELAYS_MS`    | [5000, 10000, 30000, 60000, 300000] | Escalating retry delays (5s to 5min)               |

### COMMS_RETRY (RabbitMQ Retry Topology)

| Constant             | Value                               | Purpose                                             |
| -------------------- | ----------------------------------- | --------------------------------------------------- |
| `RETRY_COUNT_HEADER` | `x-retry-count`                     | Header tracking current retry attempt number        |
| `REQUEUE_EXCHANGE`   | `comms.retry.requeue`               | Exchange routing expired tier messages back to main |
| `TIER_QUEUE_PREFIX`  | `comms.retry.tier-`                 | Prefix for tier delay queues                        |
| `TIER_TTLS`          | [5000, 10000, 30000, 60000, 300000] | TTL per tier, matches RETRY_POLICY.DELAYS_MS        |
| `DELIVERY_FAILED`    | `DELIVERY_FAILED`                   | Error code for retryable delivery failures          |

### THREAD_CONSTRAINTS

| Constant                      | Value      | Purpose                     |
| ----------------------------- | ---------- | --------------------------- |
| `MAX_TITLE_LENGTH`            | 255        | Max title/slug length       |
| `MAX_MESSAGE_LENGTH`          | 50,000     | Max content length          |
| `MAX_ATTACHMENTS_PER_MESSAGE` | 20         | Max attachments per message |
| `MAX_FILE_SIZE_BYTES`         | 52,428,800 | Max attachment size (50 MB) |
| `MAX_REACTION_LENGTH`         | 64         | Max reaction string length  |

## Tests

All tests are in `@d2/comms-tests` (`backends/node/services/comms/tests/`):

```
src/unit/domain/
  constants.test.ts
  enums/          channel.test.ts, urgency.test.ts, delivery-status.test.ts,
                  content-format.test.ts, notification-policy.test.ts, thread-type.test.ts,
                  thread-state.test.ts, participant-role.test.ts
  exceptions/     comms-domain-error.test.ts, comms-validation-error.test.ts
  entities/       message.test.ts, delivery-request.test.ts, delivery-attempt.test.ts,
                  channel-preference.test.ts, message-receipt.test.ts, thread.test.ts,
                  thread-participant.test.ts, message-attachment.test.ts, message-reaction.test.ts
  rules/          channel-resolution.test.ts, retry-policy.test.ts, thread-permissions.test.ts
```

Run: `pnpm vitest run --project comms-tests`

## Dependencies

| Package         | Purpose                 |
| --------------- | ----------------------- |
| `@d2/utilities` | String cleaning, UUIDv7 |
