<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.Events

> Parent: [`server/shared/dotnet/`](../../README.md)

Cross-service auth-lifecycle **event DTOs** — the published message types one service emits and others consume to refresh in-process state. Today this is a single type, `KeyRotatedEvent` (published by KeyCustodian whenever a key changes lifecycle state; consumed by auth middleware and JWKS-refresh jobs to re-pull their key rings).

This is a pure-vocabulary leaf: no impl logic, no DI extension, no `Add*` registration. A publisher attaches the type to an `IMessageBus.PublishAsync` call; a consumer subscribes via `[MqSub]`. Both reference this lib for the event types — never each other's service-internal assemblies (a cross-service event must not couple consumers to a producer's internals).

---

## File layout

| Path                  | Contents                                                                                                                                                                                                                                                                                          |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `KeyRotatedEvent.cs`  | `sealed record KeyRotatedEvent` — `Domain` / `Kid` / `NewStatus` (wire-stable strings) + `Urgent` (bool). Carries `[MqPub(MqMessages.AuthKeyRotated)]` directly on the record. Published plaintext to the `d2.security.key-rotated` fanout exchange (public identifiers only — no key material).  |

---

## Why a standalone leaf lib (not folded into `auth/abstractions`)

Each event type carries an `[MqPub(MqMessages.*)]` attribute. The publisher's `MessageWireResolver` reads that attribute off the **runtime CLR type** via reflection and fails default-deny if it is absent — so the attribute MUST sit on the exact single `sealed record` that gets instantiated and published. That forces a reference to `D2.Shared.Messaging.Abstractions`.

[`auth/abstractions`](../abstractions/README.md) **cannot** take that reference. There is a real dependency chain:

```
Messaging.Abstractions → Handler → Context.Abstractions → AuthContext.Abstractions → Auth.Abstractions
```

If `auth/abstractions` referenced `Messaging.Abstractions`, that chain would close into a cycle. This leaf is the cycle-break: nothing in the messaging chain references it, so it can depend on `Messaging.Abstractions` freely. The event's wire-contract namespace stays `D2.Shared.Auth.Events` (matching the `messageType` declared in `contracts/mq-messages/mq-messages.spec.json`).

---

## Dependencies

- `D2.Shared.Messaging.Abstractions` — the `[MqPub(MqMessages.X)]` attribute + the generated `MqMessages` constants each event references.

No other runtime deps. The `MqMessages.AuthKeyRotated` constant + the `d2.security.key-rotated` exchange / `plaintext` encryption decision are declared once in `contracts/mq-messages/mq-messages.spec.json` and codegen-emitted into `MqMessagesRegistry`.

---

## Tests

`server/shared/dotnet/tests/Unit/Auth/Events/`:

- `KeyRotatedEventTests.cs` — `[MqPub]` presence on the published CLR type, the resolved descriptor (exchange / fanout / plaintext / `messageType` FQN match), and record equality / required-property shape.

Run: `dotnet test server/shared/dotnet/tests`.
