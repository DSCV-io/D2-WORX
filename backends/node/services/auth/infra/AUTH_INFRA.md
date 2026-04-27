# @d2/auth-infra

BetterAuth configuration, Drizzle ORM schema/migrations, Redis secondary storage, repository handlers, and infrastructure hooks for the Auth service. This is the **only** package that imports `better-auth`.

## Purpose

Implements the infrastructure layer for authentication: BetterAuth instance creation, database schema (both BetterAuth-managed and custom tables), repository handlers for custom entities, Redis-backed secondary session storage, sign-in throttle store, password validation hooks, and mappers from BetterAuth row types to domain types.

## Design Decisions

| Decision                          | Rationale                                                                                                                 |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| BetterAuth is infra, not domain   | BetterAuth internals never leave this package — all data crosses via domain types                                         |
| Drizzle adapter for BetterAuth    | Single ORM for all 11 tables (ADR-009). `drizzleAdapter(db, { provider: "pg" })`                                          |
| AuthHooks callback interface      | Decouples infra from app layer — composition root wires app-layer callbacks in                                            |
| No direct ioredis imports         | Secondary storage + throttle use `@d2/interfaces` cache handler abstractions                                              |
| Structural typing for throttle    | `SignInThrottleStore` matches `ISignInThrottleStore` shape without importing it                                           |
| Repository handlers follow TLC    | Same `c/`, `r/`, `u/`, `d/` folder convention as app-layer and .NET patterns                                              |
| Service keys re-exported from app | Keys live alongside interfaces in auth-app; infra re-exports for convenience                                              |
| Mappers as plain functions        | `toDomainX(row)` converters — no class overhead, easy to test. Convert DB `null` → `undefined` for optional domain fields |
| Password hooks wrap BetterAuth    | Domain validation + HIBP check before BetterAuth's own bcrypt hashing                                                     |

## Package Structure

```
src/
  index.ts                  Barrel exports
  registration.ts           addAuthInfra(services, db) DI registration
  service-keys.ts           Re-exports all service keys from @d2/auth-app
  auth/
    better-auth/
      auth-factory.ts       createAuth() — single BetterAuth configuration point
      auth-config.ts        AuthServiceConfig interface + AUTH_CONFIG_DEFAULTS
      access-control.ts     RBAC: ac, ownerPermissions, officerPermissions, agentPermissions, auditorPermissions
      secondary-storage.ts  createSecondaryStorage() — Redis via @d2/interfaces handlers
      hooks/
        id-hooks.ts         generateId() — UUIDv7 for all BetterAuth entities
        org-hooks.ts        beforeCreateOrganization — enforces org type defaults
        username-hooks.ts   ensureUsername — populates username from name/email if missing
        password-hooks.ts   createPasswordFunctions, checkBreachedPassword (HIBP k-anonymity)
    sign-in-throttle-store.ts  SignInThrottleStore — Redis-backed (Exists, GetTtl, Set, Remove, Increment)
  mappers/
    user-mapper.ts          toDomainUser(row)
    org-mapper.ts           toDomainOrganization(row)
    session-mapper.ts       toDomainSession(row)
    member-mapper.ts        toDomainMember(row)
    invitation-mapper.ts    toDomainInvitation(row)
  repository/
    migrate.ts              runMigrations(pool) — Drizzle migrator (idempotent)
    utils/
      pg-errors.ts          PostgreSQL error code helpers (e.g., unique violation 23505)
    schema/
      index.ts              Re-exports all table definitions + row types
      better-auth-tables.ts 8 BetterAuth-managed tables (user, session, account, verification, jwks, organization, member, invitation)
      custom-tables.ts      3 custom tables (sign_in_event, emulation_consent, org_contact)
      types.ts              Row insert/select types for custom tables
    handlers/
      factories.ts          createSignInEventRepoHandlers, createEmulationConsentRepoHandlers, createOrgContactRepoHandlers
      c/
        create-sign-in-event.ts             Insert sign_in_event row
        create-emulation-consent-record.ts  Insert emulation_consent row
        create-org-contact-record.ts        Insert org_contact row
      r/
        find-sign-in-events-by-user-id.ts   Paginated query, ordered by createdAt DESC
        count-sign-in-events-by-user-id.ts  Count by userId
        get-latest-sign-in-event-date.ts    Max createdAt for a userId (cache staleness check)
        find-emulation-consent-by-id.ts     Single consent by ID
        find-active-consents-by-user-id.ts  Active (non-revoked, non-expired) consents
        find-active-consent-by-user-id-and-org.ts  Duplicate check for consent creation
        find-org-contact-by-id.ts           Single junction record by ID
        find-org-contacts-by-org-id.ts      Paginated junctions for an org
        check-org-exists.ts                 Boolean existence check by org ID
        get-deleted-users-to-purge.ts       Cursor-batched id list of pending_deletion users past grace
        check-sole-owner-orgs.ts            Org ids where userId is the sole `owner`
      u/
        revoke-emulation-consent-record.ts  Sets revokedAt timestamp
        update-org-contact-record.ts        Updates label/isPrimary + updatedAt
        update-user-status.ts               UPDATE user.status (+ optional deleted_at, deletion_feedback)
        anonymize-user.ts                   Single-tx PII scrub of user + cascade DELETE on account/session
      d/
        delete-org-contact-record.ts              Deletes junction row by ID
        delete-all-user-sessions.ts               DELETE FROM session WHERE user_id = ? RETURNING id
        purge-expired-sessions.ts                 Batch-deletes sessions past expiresAt
        purge-sign-in-events.ts                   Batch-deletes sign-in events before cutoff
        purge-expired-invitations.ts              Batch-deletes invitations past expiresAt
        purge-expired-emulation-consents.ts       Batch-deletes expired/revoked consents
    migrations/
      0000_init.sql          Initial migration for custom tables
```

## BetterAuth Configuration

`createAuth()` produces a fully configured BetterAuth instance with:

### Plugins

| Plugin         | Purpose                                                             |
| -------------- | ------------------------------------------------------------------- |
| `bearer`       | Session token via `Authorization: Bearer` header (for mobile/API)   |
| `username`     | Username support on user accounts                                   |
| `jwt`          | RS256 JWT issuance with JWKS endpoint, 15min expiry, custom payload |
| `organization` | Multi-org with custom roles + RBAC access control                   |
| `admin`        | Admin features + user impersonation (1hr session duration)          |

### Session Settings

| Setting                  | Default Value       |
| ------------------------ | ------------------- |
| `expiresIn`              | 604,800s (7 days)   |
| `updateAge`              | 86,400s (1 day)     |
| `cookieCache.maxAge`     | 300s (5 minutes)    |
| `cookieCache.strategy`   | `"compact"`         |
| `storeSessionInDatabase` | `true` (dual-write) |
| `cookieOptions.sameSite` | `"lax"`             |

### Session Extension Fields

| Field                      | Source                                 |
| -------------------------- | -------------------------------------- |
| `activeOrganizationType`   | Enriched on org switch (DB lookup)     |
| `activeOrganizationRole`   | Enriched on org switch (member lookup) |
| `emulatedOrganizationId`   | Set by emulation flow                  |
| `emulatedOrganizationType` | Set by emulation flow                  |

### JWT Payload Claims

Custom `definePayload` emits: `sub`, `email`, `username`, `orgId`, `orgType`, `role`, `emulatedOrgId`, `isEmulating`, `impersonatedBy`, `isImpersonating`, `impersonatingEmail`, `impersonatingUsername`, `fp` (fingerprint).

### Database Hooks

| Hook                    | Purpose                                                            |
| ----------------------- | ------------------------------------------------------------------ |
| `user.create.before`    | Ensures username, pre-generates UUIDv7, creates Geo contact        |
| `session.update.before` | Enriches activeOrganizationType + activeOrganizationRole on switch |
| `session.create.after`  | Records sign-in audit event (fire-and-forget)                      |

### AuthHooks Callback Interface

The composition root provides these callbacks to avoid circular dependencies between infra and app layers:

| Callback                                | Purpose                                                    |
| --------------------------------------- | ---------------------------------------------------------- |
| `onSignIn`                              | Records audit event after successful sign-in               |
| `getFingerprintForCurrentRequest`       | Returns client fingerprint for JWT `fp` claim              |
| `getDeviceFingerprintForCurrentRequest` | Returns device fingerprint for sign-in event audit         |
| `passwordFunctions`                     | Custom hash/verify with domain validation + HIBP check     |
| `publishVerificationEmail`              | Publishes verification email event to RabbitMQ             |
| `publishPasswordReset`                  | Publishes password reset email event to RabbitMQ           |
| `createUserContact`                     | Creates Geo contact before user record (fail-fast pattern) |

## RBAC Access Control

Hierarchical role permissions (each level inherits from below):

| Role      | Resources                                                                                      |
| --------- | ---------------------------------------------------------------------------------------------- |
| `auditor` | businessData:read, orgSettings:read                                                            |
| `agent`   | + businessData:create/update                                                                   |
| `officer` | + businessData:delete, billing:read/update, member:create/read/update                          |
| `owner`   | + orgSettings:update, organization:read/update/delete, member:delete, invitation:create/cancel |

## Secondary Storage

`createSecondaryStorage()` wraps `@d2/interfaces` distributed cache handlers behind BetterAuth's `SecondaryStorage` contract (`{ get, set, delete }`). TTL conversion: BetterAuth passes seconds, handlers expect milliseconds.

## Sign-In Throttle Store

`SignInThrottleStore` — Redis-backed implementation of `ISignInThrottleStore` using five `@d2/cache-redis` handler abstractions:

| Method                | Redis Pattern                         | Key Prefix         |
| --------------------- | ------------------------------------- | ------------------ |
| `isKnownGood`         | EXISTS                                | `signin:known:`    |
| `getLockedTtlSeconds` | GET TTL                               | `signin:locked:`   |
| `markKnownGood`       | SET with 90-day TTL                   | `signin:known:`    |
| `incrementFailures`   | INCR with 15-min window TTL           | `signin:attempts:` |
| `setLocked`           | SET with computed delay TTL           | `signin:locked:`   |
| `clearFailureState`   | DEL attempts + locked keys (parallel) | both               |

## Password Validation

`createPasswordFunctions()` returns `{ hash, verify }` for BetterAuth's `emailAndPassword.password`:

1. **Domain validation** (sync) — numeric-only, date-like, common blocklist checks
2. **HIBP breach check** (async, fail-open) — SHA-1 k-anonymity API with 24hr prefix cache
3. **BetterAuth bcrypt** — delegates to built-in `hashPassword()` for final hashing

`verify` is a pass-through to BetterAuth's bcrypt verifier (no custom logic on login).

## Drizzle Schema

### BetterAuth-Managed Tables (8)

`user`, `session`, `account`, `verification`, `jwks`, `organization`, `member`, `invitation`

### Custom Tables (3)

| Table               | Columns                                                                                    | Indexes                                                                                    |
| ------------------- | ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------ |
| `sign_in_event`     | id, user_id, successful, ip_address, user_agent, who_is_id, device_fingerprint, created_at | idx_sign_in_event_user_id                                                                  |
| `emulation_consent` | id, user_id, granted_to_org_id, expires_at, revoked_at, created_at                         | idx_emulation_consent_user_id, unique(user_id, granted_to_org_id) WHERE revoked_at IS NULL |
| `org_contact`       | id, organization_id, label, is_primary, created_at, updated_at                             | idx_org_contact_organization_id                                                            |

## Purge Repository Handlers

Four batch-delete handlers for scheduled job cleanup. All use `batchDelete` from `@d2/batch-pg` (select IDs in batches, delete by `IN` clause, loop until zero rows remain):

| Handler                         | Table               | Where Clause                                 | Notes                            |
| ------------------------------- | ------------------- | -------------------------------------------- | -------------------------------- |
| `PurgeExpiredSessions`          | `session`           | `expiresAt < now()`                          | BetterAuth-managed table         |
| `PurgeSignInEvents`             | `sign_in_event`     | `createdAt < cutoffDate`                     | Cutoff computed by job handler   |
| `PurgeExpiredInvitations`       | `invitation`        | `expiresAt < cutoffDate`                     | BetterAuth-managed table         |
| `PurgeExpiredEmulationConsents` | `emulation_consent` | `expiresAt < now() OR revokedAt IS NOT NULL` | Removes both expired and revoked |

## User-Deletion Repository Handlers

Five handlers backing the self-service deletion + nightly anonymization pipeline. Schema additions in migration `0011_add_user_deletion_fields.sql`: `user.status` (`'active' | 'pending_deletion' | 'deleted'`, default `'active'`), `user.deleted_at` (grace clock — also reused as the actual anonymization timestamp post-finalize), `user.deletion_feedback` (jsonb `{ reason?, comment? }`), and partial index `user_pending_deletion_idx ON deleted_at WHERE status='pending_deletion'`.

| Handler                  | TLC | Operation                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| ------------------------ | --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `UpdateUserStatus`       | u/  | UPDATE `user.status` plus optional `deleted_at` / `deletion_feedback`. Returns `{ updated: bool }` (false if no row matched)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| `GetDeletedUsersToPurge` | r/  | Internal cursor loop (`DEFAULT_BATCH_SIZE` from `@d2/batch-pg`) over `status='pending_deletion' AND deleted_at < graceCutoff`. Returns flat `string[]` of user ids. Backed by `user_pending_deletion_idx`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `AnonymizeUser`          | u/  | Single transaction with `WHERE status='pending_deletion'` guard (race-safe vs concurrent `CancelUserDeletion`). Captures `originalEmail`/`originalName` BEFORE the scrub via SELECT-then-UPDATE in the same tx (returned to the caller for the fanout event). Scrub: `status='deleted'`, `name='Deleted user'`, `email='deleted-{id}@deleted.local'`, `username`/`displayUsername='deleted_{id}'`, `image`/`phone` NULL, `phoneVerified=false`. DELETEs `account` + `session` rows (frees Google sub / credential for re-registration). UPDATEs `sign_in_event` SET `ip_address`/`user_agent='[anonymized]'`, `device_fingerprint=null`, `who_is_id=null`. Keeps `role`, `createdAt`, `status`, `deletedAt`, `deletionFeedback`, `signInEvent.successful` + `createdAt` for retention metrics |
| `CheckSoleOwnerOrgs`     | r/  | Single SQL with subquery counting owners per candidate org. Returns `string[]` of orgIds where the user is the SOLE `owner`. Empty array = safe to delete; non-empty = `RequestUserDeletion` returns 409 `SOLE_OWNER_OF_ORGS`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| `DeleteAllUserSessions`  | d/  | `DELETE FROM session WHERE user_id = ? RETURNING id`. Returns `{ rowsAffected }`. Used by `RequestUserDeletion` to terminate every active session before the grace window starts                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |

## Repository Handler Patterns

Concrete examples of the most commonly missed repository patterns.

### Drizzle UPDATE with `.returning()` + notFound

From `RevokeEmulationConsentRecord` — **always** chain `.returning()` and check the result array:

```typescript
protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
  const rows = await this.db
    .update(emulationConsent)
    .set({ revokedAt: new Date() })
    .where(eq(emulationConsent.id, input.id))
    .returning({ id: emulationConsent.id });

  if (rows.length === 0) return D2Result.notFound();

  return D2Result.ok({ data: {} });
}
```

Without `.returning()`, the query succeeds silently even if the row doesn't exist, and you'd return `ok()` for a no-op — a bug.

### Drizzle DELETE with `.returning()` + notFound

From `DeleteOrgContactRecord` — identical pattern to UPDATE:

```typescript
protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
  const rows = await this.db
    .delete(orgContact)
    .where(eq(orgContact.id, input.id))
    .returning({ id: orgContact.id });

  if (rows.length === 0) return D2Result.notFound();

  return D2Result.ok({ data: {} });
}
```

### DI Registration Pattern

From `registration.ts` — each handler is registered as transient with its DB + context dependencies:

```typescript
export function addAuthInfra(services: ServiceCollection, db: NodePgDatabase): void {
  services.addTransient(
    ICreateSignInEventKey,
    (sp) => new CreateSignInEvent(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IRevokeEmulationConsentRecordKey,
    (sp) => new RevokeEmulationConsentRecord(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IDeleteOrgContactRecordKey,
    (sp) => new DeleteOrgContactRecord(db, sp.resolve(IHandlerContextKey)),
  );
  // ... more handlers ...
}
```

**Checklist when adding a new handler:**

1. Create the interface in `@d2/auth-app` interfaces
2. Create the implementation in `@d2/auth-infra` repository handlers
3. Add `ServiceKey` in `service-keys.ts`
4. Add `services.addTransient()` in `registration.ts` — **missing this is a silent runtime crash**

## DI Registration

```typescript
addAuthInfra(services: ServiceCollection, db: NodePgDatabase): void
```

Registers all repository handlers (including `CheckOrgExists` via `ICheckOrgExistsKey`) as **transient** (new instance per resolve). Each handler receives the Drizzle `db` instance and scoped `IHandlerContext` from the container.

## Dependencies

| Package           | Purpose                                             |
| ----------------- | --------------------------------------------------- |
| `@d2/auth-app`    | Repository interface types and service keys         |
| `@d2/auth-domain` | Domain entities, constants, password validation     |
| `@d2/batch-pg`    | Batched delete utility for purge handlers           |
| `@d2/di`          | `ServiceCollection` for DI registration             |
| `@d2/handler`     | `BaseHandler`, `IHandlerContext`                    |
| `@d2/interfaces`  | Distributed cache handler types (secondary storage) |
| `@d2/result`      | `D2Result` for handler return types                 |
| `@d2/utilities`   | `generateUuidV7`                                    |
| `better-auth`     | Auth framework (plugins, adapter, crypto)           |
| `drizzle-orm`     | ORM for PostgreSQL (schema, queries, migrations)    |
| `pg`              | PostgreSQL client (Pool for migrations)             |

## Tests

All tests are in `@d2/auth-tests` (`backends/node/services/auth/tests/`):

```
src/unit/infra/
  access-control.test.ts
  password-hooks.test.ts
  secondary-storage.test.ts
  sign-in-throttle-store.test.ts
  username-hooks.test.ts
  mappers/
    user-mapper.test.ts, org-mapper.test.ts, session-mapper.test.ts,
    member-mapper.test.ts, invitation-mapper.test.ts

src/integration/
  auth-factory.test.ts              BetterAuth instance creation + plugin config
  better-auth-behavior.test.ts      BetterAuth API method behavior validation
  better-auth-tables.test.ts        BetterAuth-managed table schema verification
  custom-table-repositories.test.ts  CRUD operations on custom tables
  migration.test.ts                  Drizzle migration idempotency
  secondary-storage.test.ts          Redis secondary storage round-trip
  session-fingerprint.test.ts        Session fingerprint binding + revocation
  sign-in-throttle-store.test.ts     Redis throttle store operations
```

Run: `pnpm vitest run --project auth-tests`
