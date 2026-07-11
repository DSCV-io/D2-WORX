<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0016: KeyCustodian — key lifecycle state machine + dedicated leaderless store

- **Status**: Accepted
- **Date**: 2026-06-06 (store-mechanism revision: 2026-06-09; DB name: 2026-07-10)
- **Deliverable**: `0016-keycustodian`

### Amendment — 2026-07-10: database name `d2-keycustodian`

The PostgreSQL database name is **`d2-keycustodian`** (canonical D2 form `d2-{domain}`). The earlier working name `keycustodian_db` is retired. The same scheme applies to future domain DBs (`d2-auth`, `d2-files`, …). Legacy v1 leftovers may still use `d2-services-*` until those services rebuild. Advisory-lock catalog + env templates use the new name; the connection string path segment is `…/d2-keycustodian`.

## Context

The Edge service requires a key authority that is distinct from the auth service: auth consumes managed keys to sign JWTs and verify them; it cannot be the authority responsible for those keys' lifecycle without creating a circular dependency. The forces shaping the design are:

- **JWKS signing-key rotation**: the JWKS endpoint must continue serving the *retiring* key's public material during the grace window so in-flight tokens remain valid after rotation.
- **Payload-encryption key overlap**: `AesPayload` keys used to encrypt RabbitMQ payloads must still decrypt historical messages after a new key activates.
- **Cookie and client-secret material**: symmetric keys for session-cookie signing and client-secret derivation follow the same lifecycle pattern.
- **Make-illegal-states-unrepresentable**: an early design sketched key state as an enum with nullable timestamps — anemic models that represent illegal states (e.g. an "Active" key without an `ActivatedAt`) and drift when checks are only in tests, not types.
- **Leaderless rotation**: the rotation coordinator must not be a single point of failure. Redis is a hot-path dependency already shared across services; using it as the distributed lock for a low-frequency operation (key rotation) adds unnecessary coupling. PostgreSQL is already owned for KeyCustodian storage.

An early design sketched Redis-coordinated rotation with an anemic enum model. This ADR supersedes that sketch.

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

### 2. `KeyStatus` enum — derived state-machine discriminator + persisted `Status` value

The `KeyStatus` enum is derived from the concrete sealed type at the domain level — each sealed state overrides `EncryptionKey.Status` with a compile-time constant, so the enum is NEVER assigned directly from domain business logic. It serves two roles:

- **In the domain**: a fast, read-only discriminator for lookups and audit stamping, always read off the sealed type.
- **In persistence**: the `Status` **value** column of the flat `KeyRecord` (see §7) — a settable string/enum value, NOT a TPH type discriminator. The mapper sets it from `aggregate.Status` on write and switches on it on read. The persistence shape is settled in ADR-0017 (the aggregate is NOT the EF entity).

### 3. Dedicated `d2-keycustodian` — leaderless, PG advisory lock

KeyCustodian persists to its own PostgreSQL database (`d2-keycustodian`). This is independent of the auth DB and any other service DB, following the one-DB-per-domain topology.

Rotation coordination uses **PostgreSQL advisory locks** (`pg_try_advisory_lock`) instead of Redis. Rationale: (a) advisory locks are transactional — the lock is held for the duration of the rotation transaction, preventing a second rotation from interleaving; (b) PG is already provisioned for this service; (c) Redis is a hot-path dependency whose failure would block key rotation unnecessarily; (d) key rotation is a rare operation that does not require Redis's sub-millisecond locking overhead.

### 4. Crypto reuses `D2.Shared.Encryption`

Key material is wrapped by the root key using `D2.Shared.Encryption`'s `IPayloadCrypto.Encrypt`. The root key is a 32-byte AES key managed by KeyCustodian itself (bootstrapped once, stored at `secrets/keycustodian/root.key` (file-backed, loaded at startup via `FileRootKeyProvider`)). The Domain receives and holds already-encrypted bytes in `KeyMaterialEncrypted` — it never touches plaintext key bytes.

### 5. Material retention through all states

Encrypted key material is retained through `RetiredKey` and `CompromisedKey`:

- **Retired keys** must decrypt historical payloads encrypted before rotation (overlap decryption).
- **Retiring keys** serve their public key via JWKS during the grace window.
- **Compromised keys** material is retained for forensic investigation.

Dropping material on retire or compromise would break overlap decryption, grace-window JWKS, and forensics. At-rest GDPR erasure (`[Anonymizable]`) is not applicable to cryptographic key bytes.

### 6. Append-only `EncryptionKeyAudit`

Every lifecycle transition appends an `EncryptionKeyAudit` record carrying the `Kid`, action, resulting status, and timestamp. The audit record deliberately carries NO key material and NO free-text compromise reason — forensics via lifecycle sequence, not by replaying bytes.

The `EncryptionKeyAudit` record is written in the **SAME `SaveChangesAsync` / transaction** as the state change, so a transition + its audit entry are atomic. EF orders the principal (`KeyRecord` UPDATE) before the dependent (audit INSERT); the audit FK uses `OnDelete(Restrict)` so audit records can never be cascade-deleted out from under the lifecycle history.

### 7. Persistence — flat `KeyRecord` + pure mapper, NOT TPH (per ADR-0017)

The immutable sum-type aggregate is persisted as a single **flat, non-polymorphic** `KeyRecord` EF entity, bridged to the sealed domain states by a pure static mapper. The aggregate is NOT the EF entity. This follows ADR-0017's Shape B convention for state-machine aggregates — the persistence-strategy spike (EF Core 10.0.7 / Npgsql.EFC 10.0.1 / Postgres 17) falsified the original TPH delete+insert plan and validated this shape 6/6.

`KeyRecord` schema (App layer; mapped by the `IEntityTypeConfiguration<KeyRecord>` in Infra):

| Column | CLR type | Notes |
| --- | --- | --- |
| `kid` | `string` (PK) | from the `Kid` VO via a value converter; `Kid.FromTrusted` on read |
| `key_domain` | `string` | from `KeyDomain` VO; `KeyDomain.FromTrusted` on read |
| `key_type` | `KeyType` | enum value column |
| `key_material_encrypted` | `byte[]` | root-wrapped material (`KeyMaterialEncrypted.FromTrusted` on read); never logged |
| `public_key_material` | `byte[]?` | asymmetric only; null for symmetric |
| `created_at` | `Instant` | always present |
| `status` | `KeyStatus` | **settable value column** (string/enum) — NOT a TPH discriminator |
| `activated_at` | `Instant?` | set for `Active` / `Retiring` / `Retired` |
| `retiring_at` | `Instant?` | set for `Retiring` / `Retired` |
| `retired_at` | `Instant?` | set for `Retired` |
| `compromised_at` | `Instant?` | set for `Compromised` |
| `compromise_reason` | `string?` (≤512) | set for `Compromised`; `[RedactData]` in the domain |
| `xmin` | `xid` | Postgres system-column optimistic-concurrency token |

The pure mapper:
- `KeyRecord.ToDomain()` switches on `status` and rehydrates the correct sealed state (`PendingKey` / `ActiveKey` / `RetiringKey` / `RetiredKey` / `CompromisedKey`) from the columns via each VO's `FromTrusted` and the states' `required init` properties.
- `aggregate.ProjectOnto(record)` sets `status`, then **nulls ALL per-state columns and sets only the new state's** — the discipline that prevents a stale `retiring_at` surviving an `Active`-row write.

Because the `KeyRecord` CLR type never changes, every transition is an ordinary `UPDATE` (one statement) plus the audit `INSERT` — no delete+insert, no entity morph. The `xmin` token gives an exactly-one-winner guarantee for concurrent transitions, complementing the §3 `pg_try_advisory_lock` rotation coordination. Server-side `IQueryable` extensions (`Pending()` / `Active()` / `Signing()`) filter on the `status` value column so the `WHERE` runs in Postgres.

## Consequences

**Positive:**
- Illegal lifecycle transitions are uncompilable — the type system enforces the state machine, eliminating an entire class of runtime bugs.
- Single source of lifecycle truth: the domain types ARE the state machine, and the domain stays pure / EF-free (the flat `KeyRecord` + mapper carry all persistence concern — ADR-0017 Shape B).
- Transitions persist as one ordinary `UPDATE` + one audit `INSERT` in a single transaction — no delete+insert, no entity morph. The `xmin` token gives exactly-one-winner concurrency for free.
- Leaderless rotation (PG advisory lock) eliminates SPOF and avoids Redis coupling for a rare, non-latency-sensitive operation.
- Overlap decryption and grace-window JWKS are guaranteed by the material-retention decision.

**Negative / trade-offs:**
- Two representations of the aggregate (the sealed domain shape + the flat `KeyRecord`), bridged by the pure mapper. The mapper's null-all-then-set discipline is load-bearing and must be tested per-state (round-trip + no-stale-column assertions). This is the cost of keeping the domain pure; it is mechanical and source-gen-able once the shape is proven across 2–3 aggregates (ADR-0017).
- A new PostgreSQL database (`d2-keycustodian`) must be provisioned.
- The Redis-coordination sketch from that earlier design is superseded — operators following the old plan must switch to the PG advisory lock approach.

## Alternatives considered

- **Anemic enum + nullable timestamps as the DOMAIN model** (early anemic-enum sketch): rejected because nullable timestamps in the *domain* make illegal states representable (an "Active" key with null `ActivatedAt`), and the only enforcement is tests — not the type system. Drift between the enum and the lifecycle invariants is inevitable. (Note: the flat `KeyRecord` *persistence* row in §7 deliberately uses a `status` value column + nullable per-state columns — but it is NOT the domain; the pure mapper rehydrates the sealed state and the domain types remain the single source of lifecycle truth. The anti-pattern is an anemic *domain*, not a flat *row*.)
- **Aggregate-as-TPH-entity with delete+insert transitions** (the original TPH-entity plan): rejected as unsound on EF Core 10 — the morph wall makes a same-PK delete+insert silently merge into a stale-column UPDATE, and a get-only-`Status` discriminator fails model-build. Falsified by the persistence-strategy spike; superseded by the flat `KeyRecord` + pure mapper in §7. Full rejection rationale + EF issue citations in [ADR-0017](0017-ef-as-ddd-persistence.md).
- **Redis-coordinated rotation**: rejected because Redis is a hot-path dependency and advisory locks are already available in the PostgreSQL database owned by this service. Redis locking adds operational coupling for a rare operation.
- **Storing key material outside the key record / external KMS**: out of scope. `D2.Shared.Encryption` root-wrap is the established mechanism for all service encryption.
- **Dropping material on retire/compromise**: rejected — breaks overlap decryption, grace-window JWKS, and forensics. See §5 above.

## References

- [ADR-0017](0017-ef-as-ddd-persistence.md) — the EF-as-DDD persistence convention; Shape B (flat non-polymorphic Record + pure mapper) is what §7 above persists this aggregate through, and carries the full morph-wall rejection of TPH delete+insert.
- [ADR-0018](0018-spec-driven-error-codes.md) / [ADR-0019](0019-wrapped-result-wire-model.md) — the cross-service error-code + wrapped-result conventions the KeyCustodian domain transitions surface failures through (`KeyCustodianFailures.*`).
- [ADR-0023](0023-mtls-workload-identity.md) — mTLS workload identity; its internal certificate-authority capability extends the key-lifecycle state machine and overlap-rotation model defined here with a new certificate key-type and its issuance, rotation, and revocation operations.
- Persistence-strategy spike (EF Core 10.0.7 / Npgsql.EFC 10.0.1 / Postgres 17) — 6/6 validated; throwaway Testcontainers spike falsified TPH delete+insert and confirmed the flat-record Shape B approach persisted through `KeyRecord` + pure mapper (see §7 above and ADR-0017).
