<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0016: KeyCustodian — key lifecycle state machine + dedicated leaderless store

- **Status**: Accepted (draft — finalized at SHIP of deliverable 0016)
- **Date**: 2026-06-06
- **Deliverable**: `0016-keycustodian`

## Context

Phase 3 requires a key authority that is distinct from the auth service: auth consumes managed keys to sign JWTs and verify them; it cannot be the authority responsible for those keys' lifecycle without creating a circular dependency. The forces shaping the design are:

- **JWKS signing-key rotation**: the JWKS endpoint must continue serving the *retiring* key's public material during the grace window so in-flight tokens remain valid after rotation.
- **Payload-encryption key overlap**: `AesPayload` keys used to encrypt RabbitMQ payloads must still decrypt historical messages after a new key activates.
- **Cookie and client-secret material**: symmetric keys for session-cookie signing and client-secret derivation follow the same lifecycle pattern.
- **Make-illegal-states-unrepresentable**: V2.md §5.4 sketched key state as an enum with nullable timestamps — anemic models that represent illegal states (e.g. an "Active" key without an `ActivatedAt`) and drift when checks are only in tests, not types.
- **Leaderless rotation**: the rotation coordinator must not be a single point of failure. Redis is a hot-path dependency already shared across services; using it as the distributed lock for a low-frequency operation (key rotation) adds unnecessary coupling. PostgreSQL is already owned for KeyCustodian storage.

V2.md §5.4 originally sketched a Redis-coordinated rotation with an anemic enum model. This ADR supersedes that sketch.

## Decision

### 1. Five-state sum-type lifecycle

Key lifecycle is modeled as an `abstract record EncryptionKey` base + five sealed per-state records:

```
Pending → Active → Retiring → Retired    (terminal)
                ↘
          Compromised (terminal, reachable from Pending/Active/Retiring)
```

Each sealed type (`PendingKey`, `ActiveKey`, `RetiringKey`, `RetiredKey`, `CompromisedKey`) overrides `KeyStatus Status { get; }` with a compile-time constant. Transition methods exist ONLY on the types from which the transition is legal — a `RetiredKey` exposes no `Activate`, `Rotate`, `Retire`, or `Compromise` method; attempting to call them does not compile. This is §9.31 (make-illegal-states-unrepresentable) applied at the type level.

Guarded transitions (`PendingKey.Activate`, `RetiringKey.Retire`) return `D2Result<TNext>` and validate timing windows against the rotation policy. Unguarded transitions (`ActiveKey.Rotate`, `*.Compromise`) return the next state directly.

### 2. `KeyStatus` enum — derived discriminator only

The `KeyStatus` enum is retained EXCLUSIVELY as the EF Core TPH discriminator (Step 3). It is always derived from the concrete sealed type; it is NEVER assigned directly from business logic. The Step-3 TPH mapping depends on `KeyStatus` being read-only per sealed type.

### 3. Dedicated `keycustodian_db` — leaderless, PG advisory lock

KeyCustodian persists to its own PostgreSQL database (`keycustodian_db`). This is independent of the auth DB and any other service DB, following the one-DB-per-domain topology.

Rotation coordination uses **PostgreSQL advisory locks** (`pg_try_advisory_lock`) instead of Redis. Rationale: (a) advisory locks are transactional — the lock is held for the duration of the rotation transaction, preventing a second rotation from interleaving; (b) PG is already provisioned for this service; (c) Redis is a hot-path dependency whose failure would block key rotation unnecessarily; (d) key rotation is a rare operation that does not require Redis's sub-millisecond locking overhead.

### 4. Crypto reuses `D2.Shared.Encryption`

Key material is wrapped by the root key using `D2.Shared.Encryption`'s `IPayloadCrypto.Encrypt`. The root key is a 32-byte AES key managed by KeyCustodian itself (bootstrapped once, stored in the `keycustodian_db`). The Domain receives and holds already-encrypted bytes in `KeyMaterialEncrypted` — it never touches plaintext key bytes.

### 5. Material retention through all states

Encrypted key material is retained through `RetiredKey` and `CompromisedKey`:

- **Retired keys** must decrypt historical payloads encrypted before rotation (overlap decryption).
- **Retiring keys** serve their public key via JWKS during the grace window.
- **Compromised keys** material is retained for forensic investigation.

Dropping material on retire or compromise would break overlap decryption, grace-window JWKS, and forensics. At-rest GDPR erasure (`[Anonymizable]`) is not applicable to cryptographic key bytes.

### 6. Append-only `EncryptionKeyAudit`

Every lifecycle transition appends an `EncryptionKeyAudit` record carrying the `Kid`, action, resulting status, and timestamp. The audit record deliberately carries NO key material and NO free-text compromise reason — forensics via lifecycle sequence, not by replaying bytes.

## Consequences

**Positive:**
- Illegal lifecycle transitions are uncompilable — the type system enforces the state machine, eliminating an entire class of runtime bugs.
- Single source of lifecycle truth: the domain types ARE the state machine.
- Leaderless rotation (PG advisory lock) eliminates SPOF and avoids Redis coupling for a rare, non-latency-sensitive operation.
- Overlap decryption and grace-window JWKS are guaranteed by the material-retention decision.

**Negative / trade-offs:**
- First TPH (`TABLE PER HIERARCHY`) mapping in the codebase (Step 3 sharp edge): sealed-type state transitions must be implemented as delete-old-row + insert-new-row rather than `UPDATE`, because the discriminator column maps to the CLR type.
- A new PostgreSQL database (`keycustodian_db`) must be provisioned.
- The V2.md §5.4 Redis-coordination sketch is superseded — operators following the old plan must switch to the PG advisory lock approach.

## Alternatives considered

- **Anemic enum + nullable timestamps** (V2.md §5.4 sketch): rejected because nullable timestamps make illegal states representable (an "Active" key with null `ActivatedAt`), and the only enforcement is tests — not the type system. Drift between the enum and the lifecycle invariants is inevitable.
- **Redis-coordinated rotation**: rejected because Redis is a hot-path dependency and advisory locks are already available in the PostgreSQL database owned by this service. Redis locking adds operational coupling for a rare operation.
- **Storing key material outside the key record / external KMS**: out of scope. `D2.Shared.Encryption` root-wrap is the established mechanism for all service encryption.
- **Dropping material on retire/compromise**: rejected — breaks overlap decryption, grace-window JWKS, and forensics. See §5 above.
