<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Messaging.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits the messaging registry types (`MqMessages` + `MqMessagesRegistry` + `MqSubscriptions` + `MqSubscriptionsRegistry`) into [`D2.Shared.Messaging.Abstractions`](../messaging-abstractions/README.md) by reading two spec files from `contracts/`:

- `mq-messages.spec.json` — every message type the platform publishes (constant name, runtime descriptor, encryption requirements)
- `mq-subscriptions.spec.json` — every subscription declaration (consumer name, message types, retry policy, prefetch, idempotency)

The spec files are the single source of truth — adding a new message or subscription is a one-line edit to a JSON file; the generator picks it up next build, emits the constant + descriptor + registry entry, and downstream consumers compile against typed identifiers (`MqMessages.UserCreated` not `"user.created"`).

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

---

## Build-time diagnostics

| ID | Severity | Trigger |
|---|---|---|
| `D2MQ001` | Error | Spec file is malformed JSON or violates the schema |
| `D2MQ002` | Error | Spec entry missing a required field (e.g. messages-entry missing `constant`) |
| `D2MQ003` | Error | Two messages or subscriptions declare the same constant name |
| `D2MQ004` | Error | Encryption domain referenced by a messages-entry isn't in `D2.Shared.Encryption.EncryptionDomains` (catches drift between encryption keyring registration and message declarations) |
| `D2MQ005` | Error | `plaintext`-encryption messages-entry missing the required `encryptionReason` justification |
| `D2MQ006` | Error | Subscription `pattern` value isn't in `{CompetingConsumer, FanoutExclusiveAutoDelete, DurableShared}` |
| `D2MQ007` | Error | Messages-entry `messageType` value isn't in the recognized set |
| `D2MQ008` | Error | Subscription `exchangeType` value isn't in `{fanout, topic, direct}` |
| `D2MQ009` | Error | No `mq-messages.spec.json` found in `AdditionalFiles` |
| `D2MQ010` | Error | No `mq-subscriptions.spec.json` found in `AdditionalFiles` |
| `D2MQ011` | Error | Messages-entry `constant` violates C#-identifier rules (must be `^[A-Z][A-Za-z0-9]*$`) |
| `D2MQ012` | Error | Subscription `tieredRetry.tiers[*]` value isn't a valid `HH:MM:SS` `TimeSpan` literal |

---

## Spec format — messages

```json
{
  "messages": [
    {
      "constant": "UserCreated",
      "messageType": "Event",
      "encryption": "audit",
      "defaultRoutingKey": "user.created"
    },
    {
      "constant": "PaymentReceiptIssued",
      "messageType": "Event",
      "encryption": "plaintext",
      "encryptionReason": "Receipt is downstream-published to billing-public exchange; payment details intentionally exposed to external auditors per SOC 2 retention policy.",
      "defaultRoutingKey": "billing.payment.receipt"
    }
  ]
}
```

- **`constant`**: PascalCase C# identifier; becomes `MqMessages.UserCreated` etc.
- **`messageType`**: one of the recognized message-type tokens (validated by `D2MQ007`).
- **`encryption`**: lowercase wire value of an `EncryptionDomain` entry in `D2.Shared.Encryption.EncryptionDomains` (e.g. `audit` / `notifications` / `courier` / `plaintext`) — the closed catalog comes from `contracts/encryption-domains/encryption-domains.spec.json` and a typo surfaces as `D2MQ004`.
- **`encryptionReason`**: required iff `encryption == "plaintext"`. Documents WHY the payload is intentionally unencrypted (audit trail).
- **`defaultRoutingKey`**: optional default routing key for publishers.

## Spec format — subscriptions

```json
{
  "subscriptions": [
    {
      "constant": "AuditConsumer",
      "messageTypes": ["UserCreated", "RoleAssigned"],
      "pattern": "CompetingConsumer",
      "exchangeType": "topic",
      "routingKeyBinding": "audit.*",
      "prefetch": 50,
      "idempotency": true,
      "tieredRetry": {
        "tiers": ["00:00:05", "00:01:00", "00:15:00"],
        "maxAttempts": 5
      }
    }
  ]
}
```

---

## Emitted output (two `.g.cs` files)

Both files emit into the consuming assembly (`D2.Shared.Messaging.Abstractions`) from the two specs read together:

```csharp
// MqMessages.g.cs
public static partial class MqMessages
{
    public const string UserCreated = "UserCreated";
    public const string PaymentReceiptIssued = "PaymentReceiptIssued";
    // ...
}

public static partial class MqMessagesRegistry
{
    public static readonly IReadOnlyDictionary<string, MqMessageDescriptor> ByConstant = ...;
}

// MqSubscriptions.g.cs
public static partial class MqSubscriptions { /* constants */ }
public static partial class MqSubscriptionsRegistry { /* dict */ }
```

`MqMessagesRegistry.ByConstant[name]` returns the full descriptor (`messageType`, `encryptionReason`, `defaultRoutingKey`, etc.) — used by `D2.Shared.Messaging.RabbitMq` at publish time to look up encryption + routing settings without a parallel hand-written table.

The two-file split lives in one sourcegen because publishers consume `MqMessages*` constants and subscribers consume `MqSubscriptions*` constants — both derive from the same shared encryption-domain validation pass, and keeping them in one generator ensures spec edits re-emit both files together.

---

## Spec evolution rules

The spec files ARE the contract. Adding / renaming / removing fields requires
careful coordination with deployed consumers:

- **Adding a message**: add a `messages[]` entry, mark the new class
  `[MqPub(MqMessages.X)]`, rebuild — codegen surfaces the constant. Consumers
  pick up the new constant on their next build.
- **Renaming a constant**: edit the spec, update every `[MqPub("...")]` /
  `[MqSub("...")]` reference. The resolver / registrar hard-fail on stale
  references; CI catches it.
- **Changing the wire shape of an existing message**: the message class itself
  is the source of truth. JSON serialization tolerates additive changes (new
  optional fields). Removing or renaming a field is a breaking change — bump
  to a new constant + new class until every consumer has migrated.
- **Encryption-domain change**: edit the spec. The publisher picks up the new
  descriptor on the next build; rolling-upgrade safety needs both keyrings
  registered during the cutover window.

---

## Reference

- [`docs/SRC_GEN.md`](../../../../docs/SRC_GEN.md) — canonical how-to-author guide for D² Roslyn source generators
- [`contracts/mq-messages/mq-messages.spec.json`](../../../../contracts/mq-messages/mq-messages.spec.json) — message catalog
- [`contracts/mq-subscriptions/mq-subscriptions.spec.json`](../../../../contracts/mq-subscriptions/mq-subscriptions.spec.json) — subscription catalog
- [`D2.Shared.Messaging.Abstractions`](../messaging-abstractions/README.md) — emission target + transport-agnostic contract
- [`D2.Shared.Messaging.RabbitMq`](../messaging-rabbitmq/README.md) — primary consumer (publish + consume) + canonical runtime / wire-format / topology reference
- [`D2.Shared.Auth.Scopes.SourceGen`](../auth-scopes-source-gen/) — sibling SrcGen this one mirrors (same incremental-generator + diagnostic-split pattern)
