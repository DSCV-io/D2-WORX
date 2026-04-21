# @d2/auth-app

CQRS handlers and repository interfaces for the Auth service application layer. Zero BetterAuth imports — pure business logic that depends only on domain types and shared infrastructure.

## Purpose

Defines the CQRS handler layer between the API (routes) and infrastructure (repositories, BetterAuth). Handlers validate input via Zod, enforce business rules from `@d2/auth-domain`, and delegate persistence to repository handler interfaces that are implemented in `@d2/auth-infra`.

## Design Decisions

| Decision                           | Rationale                                                                                                                       |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| Interfaces defined here, not infra | Prevents circular dependency (infra cannot import from app)                                                                     |
| Repository handler bundles         | Group related repo handlers into typed objects for factory convenience                                                          |
| Handler-per-operation              | One class per CQRS operation — matches .NET Geo pattern and `BaseHandler` model                                                 |
| Zod validation at handler boundary | `this.validateInput(schema, input)` before any persistence or external calls                                                    |
| Geo contact ops via geo-client     | Org contacts are junctions — actual contact data lives in Geo service (gRPC)                                                    |
| Fail-open throttle handlers        | All store errors swallowed — sign-in availability > throttle accuracy                                                           |
| ISignInThrottleStore interface     | Non-handler contract (stateful Redis store) — structurally implemented in infra                                                 |
| DI registration via `addAuthApp()` | Mirrors .NET `services.AddAuthApp()` — all handlers registered as transient                                                     |
| Service keys alongside interfaces  | Keys live in app (with interfaces), infra re-exports for composition root access                                                |
| Handler interface extraction       | App-layer I/O types, redaction constants, and IHandler interfaces live in separate interface files — mirrors geo-client pattern |

## Package Structure

```
src/
  index.ts                  Barrel exports + factory functions
  registration.ts           addAuthApp(services, options) DI registration
  auth-job-options.ts       AuthJobOptions interface + DEFAULT_AUTH_JOB_OPTIONS
  service-keys.ts           ServiceKey<T> tokens (21 infra + 18 app)
  cache-keys.ts             AUTH_CACHE_KEYS (email availability, sign-in events, throttle)
  file-context-keys.ts      AUTH_FILE_CONTEXT_KEYS (user_avatar, org_logo — file callback context key matching)
  interfaces/
    cqrs/
      handlers/
        index.ts                     Barrel: import * as Commands / Queries
        c/
          index.ts                           Barrel re-exports all command interfaces
          record-sign-in-event.ts            IRecordSignInEventHandler + REDACTION
          record-sign-in-outcome.ts          IRecordSignInOutcomeHandler
          create-emulation-consent.ts        ICreateEmulationConsentHandler
          revoke-emulation-consent.ts        IRevokeEmulationConsentHandler
          create-org-contact.ts              ICreateOrgContactHandler + ContactInput + REDACTION
          update-org-contact.ts              IUpdateOrgContactHandler + REDACTION
          delete-org-contact.ts              IDeleteOrgContactHandler
          create-user-contact.ts             ICreateUserContactHandler + REDACTION
          request-user-deletion.ts           IRequestUserDeletionHandler + REDACTION
          cancel-user-deletion.ts            ICancelUserDeletionHandler
          finalize-deleted-user.ts           IFinalizeDeletedUserHandler + REDACTION
          run-session-purge.ts               IRunSessionPurgeHandler
          run-sign-in-event-purge.ts         IRunSignInEventPurgeHandler
          run-invitation-cleanup.ts          IRunInvitationCleanupHandler
          run-emulation-consent-cleanup.ts   IRunEmulationConsentCleanupHandler
          cleanup-deleted-users.ts           ICleanupDeletedUsersHandler
          handle-file-processed.ts           IHandleFileProcessedHandler
        q/
          index.ts                           Barrel re-exports all query interfaces
          check-email-availability.ts        ICheckEmailAvailabilityHandler + REDACTION
          check-health.ts                    ICheckHealthHandler + ComponentHealth
          check-sign-in-throttle.ts          ICheckSignInThrottleHandler
          get-active-consents.ts             IGetActiveConsentsHandler
          get-org-contacts.ts                IGetOrgContactsHandler + HydratedOrgContact + REDACTION
          get-sign-in-events.ts              IGetSignInEventsHandler + REDACTION
    repository/
      sign-in-throttle-store.ts    ISignInThrottleStore (non-handler contract)
      handlers/
        index.ts                   Re-exports + bundle interfaces
        c/
          create-sign-in-event.ts          ICreateSignInEventHandler
          create-emulation-consent-record.ts  ICreateEmulationConsentRecordHandler
          create-org-contact-record.ts     ICreateOrgContactRecordHandler
        r/
          find-sign-in-events-by-user-id.ts       IFindSignInEventsByUserIdHandler
          count-sign-in-events-by-user-id.ts      ICountSignInEventsByUserIdHandler
          get-latest-sign-in-event-date.ts        IGetLatestSignInEventDateHandler
          find-emulation-consent-by-id.ts         IFindEmulationConsentByIdHandler
          find-active-consents-by-user-id.ts      IFindActiveConsentsByUserIdHandler
          find-active-consent-by-user-id-and-org.ts  IFindActiveConsentByUserIdAndOrgHandler
          find-org-contact-by-id.ts               IFindOrgContactByIdHandler
          find-org-contacts-by-org-id.ts          IFindOrgContactsByOrgIdHandler
          find-deleted-users-to-purge.ts          IFindDeletedUsersToPurgeHandler
          check-sole-owner-orgs.ts                ICheckSoleOwnerOrgsHandler
        u/
          revoke-emulation-consent-record.ts   IRevokeEmulationConsentRecordHandler
          update-org-contact-record.ts         IUpdateOrgContactRecordHandler
          update-user-image.ts                 IUpdateUserImageHandler
          update-org-logo.ts                   IUpdateOrgLogoHandler
          update-user-status.ts                IUpdateUserStatusHandler
          anonymize-user.ts                    IAnonymizeUserHandler + REDACTION
        d/
          delete-org-contact-record.ts                IDeleteOrgContactRecordHandler
          delete-all-user-sessions.ts                 IDeleteAllUserSessionsHandler
          purge-expired-sessions.ts                   IPurgeExpiredSessionsHandler
          purge-sign-in-events.ts                     IPurgeSignInEventsHandler
          purge-expired-invitations.ts                IPurgeExpiredInvitationsHandler
          purge-expired-emulation-consents.ts         IPurgeExpiredEmulationConsentsHandler
  implementations/
    cqrs/
      handlers/
        c/
          record-sign-in-event.ts        RecordSignInEvent
          record-sign-in-outcome.ts      RecordSignInOutcome
          create-emulation-consent.ts    CreateEmulationConsent
          revoke-emulation-consent.ts    RevokeEmulationConsent
          create-org-contact.ts          CreateOrgContact
          update-org-contact.ts          UpdateOrgContactHandler
          delete-org-contact.ts          DeleteOrgContact
          create-user-contact.ts         CreateUserContact
          request-user-deletion.ts       RequestUserDeletion
          cancel-user-deletion.ts        CancelUserDeletion
          finalize-deleted-user.ts       FinalizeDeletedUser
          run-session-purge.ts           RunSessionPurge
          run-sign-in-event-purge.ts     RunSignInEventPurge
          run-invitation-cleanup.ts      RunInvitationCleanup
          run-emulation-consent-cleanup.ts  RunEmulationConsentCleanup
          cleanup-deleted-users.ts       CleanupDeletedUsers
          handle-file-processed.ts         HandleFileProcessed
        q/
          get-sign-in-events.ts          GetSignInEvents
          get-active-consents.ts         GetActiveConsents
          get-org-contacts.ts            GetOrgContacts
          check-sign-in-throttle.ts      CheckSignInThrottle
          check-email-availability.ts    CheckEmailAvailability
          check-health.ts                CheckHealth
```

## CQRS Handlers

### Command Handlers (12)

| Handler                   | Input                                                  | Output                     | Description                                                                                                                                                                                                                                                          |
| ------------------------- | ------------------------------------------------------ | -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `RecordSignInEvent`       | userId, successful, IP, UA                             | `{ event }`                | Creates immutable audit record via domain factory + repo                                                                                                                                                                                                             |
| `RecordSignInOutcome`     | identifierHash, identityHash                           | `{ recorded }`             | Records throttle state: success marks known-good, failure increments                                                                                                                                                                                                 |
| `CreateEmulationConsent`  | userId, grantedToOrgId, expiry                         | `{ consent }`              | Validates org type, checks org exists, prevents duplicates                                                                                                                                                                                                           |
| `RevokeEmulationConsent`  | consentId, userId                                      | `{ consent }`              | Ownership check + active check before revoking                                                                                                                                                                                                                       |
| `CreateOrgContact`        | orgId, label, contact details                          | `{ contact, geoContact }`  | Creates junction then Geo contact; rollback on Geo failure                                                                                                                                                                                                           |
| `UpdateOrgContactHandler` | id, orgId, updates                                     | `{ contact, geoContact? }` | Metadata-only or contact replacement via UpdateContactsByExtKeys                                                                                                                                                                                                     |
| `DeleteOrgContact`        | id, orgId                                              | `{}`                       | IDOR check, best-effort Geo delete, then junction delete                                                                                                                                                                                                             |
| `CreateUserContact`       | userId, email, name, locale                            | `{ contact }`              | Sign-up hook: Geo contact with contextKey=auth_user. Fail-fast                                                                                                                                                                                                       |
| `RequestUserDeletion`     | userId, currentPassword, feedback?                     | `{ scheduledFor }`         | Self-service deletion gate. Verifies password atomically, runs `CheckSoleOwnerOrgs` (409 `SOLE_OWNER_OF_ORGS` if any), flips `status` → `pending_deletion` + sets `deletedAt`, calls `DeleteAllUserSessions`, busts BetterAuth Redis cookie cache, sends "scheduled" notification via `alternativeContactInfo` (security-relevant — bypasses channel preferences). Returns ISO date when permanent anonymization runs (`deletedAt + GRACE_PERIOD_DAYS`) |
| `CancelUserDeletion`      | userId                                                 | `{ cancelled }`            | Invoked fire-and-forget by BetterAuth `session.create.before` hook when a `pending_deletion` user signs back in. Idempotent — no-op if status≠pending_deletion. Flips `status` → `active`, clears `deletedAt`, sends "cancelled" notification                                                                       |
| `FinalizeDeletedUser`     | userId                                                 | `{ anonymized }`           | Per-user worker invoked by `CleanupDeletedUsers`. Calls `AnonymizeUser`; on success sends "complete" notification via `alternativeContactInfo` (Geo contact is being torn down) and publishes `auth.user-anonymize` fanout event for Geo / Comms / Files consumers    |
| `HandleFileProcessed`     | fileId, contextKey, relatedEntityId, status, variants? | `{ success }`              | Routes by contextKey: `user_avatar` → UpdateUserImage, `org_logo` → UpdateOrgLogo, others → ack. Rejected files logged and acked                                                                                                                                     |

### Job Handlers (5)

Scheduled job orchestrators that acquire a distributed lock (Redis), delegate to a repository purge handler, and release the lock in a `finally` block. All return `{ rowsAffected, lockAcquired, durationMs }` (purge jobs) or an extended shape (`CleanupDeletedUsers`). If the lock is already held, the handler returns immediately with `lockAcquired: false`.

| Handler                      | Lock Key                                      | Repo Handler                            | Cutoff Logic                                    |
| ---------------------------- | --------------------------------------------- | --------------------------------------- | ----------------------------------------------- |
| `RunSessionPurge`            | `lock:job:purge-expired-sessions`             | `IPurgeExpiredSessionsHandler`          | Sessions past `expiresAt` (BetterAuth-managed)  |
| `RunSignInEventPurge`        | `lock:job:purge-sign-in-events`               | `IPurgeSignInEventsHandler`             | Events older than `signInEventRetentionDays`    |
| `RunInvitationCleanup`       | `lock:job:cleanup-expired-invitations`        | `IPurgeExpiredInvitationsHandler`       | Invitations past `expiresAt` + retention buffer |
| `RunEmulationConsentCleanup` | `lock:job:cleanup-expired-emulation-consents` | `IPurgeExpiredEmulationConsentsHandler` | Expired OR already-revoked consents             |
| `CleanupDeletedUsers`        | `lock:job:cleanup-deleted-users`              | `IFindDeletedUsersToPurgeHandler`       | Users with `status='pending_deletion'` AND `deleted_at < now() - GRACE_PERIOD_MS` |

`CleanupDeletedUsers` orchestrates a fan-out per user instead of a single batch DELETE: it calls `FindDeletedUsersToPurge` to get the candidate id list, then `Promise.all`s `FinalizeDeletedUser` per id. Per-user failures are isolated — they count as `skipped`, not as a failed run. Returns `{ processed, anonymized, skipped, lockAcquired, durationMs, rowsAffected }` where `processed` = candidates considered, `anonymized` = successful finalizations, `skipped` = per-user failures, `rowsAffected` = `anonymized` for parity with the other purge jobs. Mapped to the gRPC `CleanupDeletedUsers` rpc on `AuthJobService`. Dkron schedule: `auth-cleanup-deleted-users` at `0 0 4 * * *` (04:00 UTC daily — runs AFTER the comms purges so when this job publishes `auth.user-anonymize`, downstream consumers hit a settled DB instead of racing other nightly cleanup).

### Query Handlers (6)

| Handler                  | Input                 | Output               | Description                                                        |
| ------------------------ | --------------------- | -------------------- | ------------------------------------------------------------------ |
| `CheckEmailAvailability` | email                 | `{ available }`      | In-memory cache with asymmetric TTLs (taken=1h, available=30s)     |
| `GetSignInEvents`        | userId, limit, offset | `{ events, total }`  | Paginated with local cache + staleness check (append-only data)    |
| `GetActiveConsents`      | userId, limit, offset | `{ consents }`       | Active (non-revoked, non-expired) emulation consents               |
| `GetOrgContacts`         | orgId, limit, offset  | `{ contacts[] }`     | Junction records hydrated with Geo contact data via ext-key lookup |
| `CheckSignInThrottle`    | identifierHash, etc.  | `{ blocked, retry?}` | Optimized Redis round-trips: 0 on local cache hit, 1 otherwise     |
| `CheckHealth`            | _(none)_              | `{ status, ... }`    | Aggregates DB, cache, and message bus pings into health report     |

## Repository Handler Interfaces

Defined as `IHandler<TInput, TOutput>` interfaces, grouped into aggregate bundles:

| Bundle                         | Handlers | Operations                                                           |
| ------------------------------ | -------- | -------------------------------------------------------------------- |
| `SignInEventRepoHandlers`      | 5        | create, findByUserId, countByUserId, getLatestEventDate, updateWhoIs |
| `EmulationConsentRepoHandlers` | 5        | create, findById, findActiveByUserId, findByUserAndOrg, revoke       |
| `OrgContactRepoHandlers`       | 5        | create, findById, findByOrgId, update, delete                        |

Plus 5 standalone user-deletion handlers (not bundled — consumed directly by the deletion CQRS handlers via DI):

| Interface                          | Input                                       | Output                | Description                                                                                                                                       |
| ---------------------------------- | ------------------------------------------- | --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IUpdateUserStatusHandler`         | userId, status, deletedAt?, feedback?       | `{ updated: bool }`   | UPDATE `user.status` (+ optional `deleted_at`, `deletion_feedback`). Returns whether the row matched                                              |
| `IFindDeletedUsersToPurgeHandler`  | graceCutoff (Date)                          | `string[]` (user ids) | Internal cursor loop (`DEFAULT_BATCH_SIZE`) over `status='pending_deletion' AND deleted_at < graceCutoff`. Backed by `user_pending_deletion_idx`  |
| `IAnonymizeUserHandler`            | userId                                      | `{ originalEmail, originalName }` | Single transaction with `WHERE status='pending_deletion'` guard (race-safe vs concurrent cancel). Captures pre-scrub PII for the fanout event     |
| `ICheckSoleOwnerOrgsHandler`       | userId                                      | `string[]` (org ids)  | Single SQL with subquery counting owners per candidate org. Returns orgs where the user is the SOLE `owner`                                       |
| `IDeleteAllUserSessionsHandler`    | userId                                      | `{ rowsAffected }`    | `DELETE FROM session WHERE user_id = ? RETURNING id`. Used by `RequestUserDeletion` to terminate every active session                             |

Plus 4 standalone purge handlers (not bundled — used by job handlers via DI):

| Interface                               | Input      | Output             | Description                                   |
| --------------------------------------- | ---------- | ------------------ | --------------------------------------------- |
| `IPurgeExpiredSessionsHandler`          | _(empty)_  | `{ rowsAffected }` | Deletes sessions past `expiresAt`             |
| `IPurgeSignInEventsHandler`             | cutoffDate | `{ rowsAffected }` | Deletes sign-in events before cutoff          |
| `IPurgeExpiredInvitationsHandler`       | cutoffDate | `{ rowsAffected }` | Deletes invitations past `expiresAt` + buffer |
| `IPurgeExpiredEmulationConsentsHandler` | _(empty)_  | `{ rowsAffected }` | Deletes expired or revoked emulation consents |

Plus `ISignInThrottleStore` (non-handler interface with 6 methods for Redis key operations).

## Service Keys

53 `ServiceKey<T>` tokens organized in two groups:

- **30 infra-layer keys** — for repository handlers (including `IUpdateUserImageKey`, `IUpdateOrgLogoKey`, `ICheckOrgExistsKey`, `IUpdateUserStatusKey`, `IFindDeletedUsersToPurgeKey`, `IAnonymizeUserKey`, `ICheckSoleOwnerOrgsKey`, `IDeleteAllUserSessionsKey`), PingDb, throttle store, and 4 purge handlers (interfaces defined here, implemented in `@d2/auth-infra`)
- **23 app-layer keys** — for CQRS handlers including CheckEmailAvailability, CheckHealth, HandleFileProcessed, the 3 user-deletion command handlers (`IRequestUserDeletionKey`, `ICancelUserDeletionKey`, `IFinalizeDeletedUserKey`), and 5 job handlers (typed against handler interfaces, e.g., `Commands.IRecordSignInEventHandler`)

## AuthJobOptions

Configuration for scheduled job handlers, provided via `addAuthApp()` (defaults to `DEFAULT_AUTH_JOB_OPTIONS`):

| Property                   | Type     | Default  | Description                                         |
| -------------------------- | -------- | -------- | --------------------------------------------------- |
| `signInEventRetentionDays` | `number` | `90`     | Days to retain sign-in events before purge          |
| `invitationRetentionDays`  | `number` | `7`      | Days past expiry to retain invitations before purge |
| `jobLockTtlMs`             | `number` | `300000` | Distributed lock TTL in milliseconds (5 min)        |

## DI Registration

```typescript
addAuthApp(services: ServiceCollection, jobOptions?: AuthJobOptions): void
```

Registers all 23 CQRS handlers as **transient** (new instance per resolve). Each handler receives its repository dependencies and `IHandlerContext` from the DI container. Organization existence checks use the `ICheckOrgExistsHandler` repository handler (registered in auth-infra) via DI -- the `AddAuthAppOptions` parameter was removed (org existence is now a DI-resolved repo handler instead of a callback). The optional `jobOptions` parameter (defaults to `DEFAULT_AUTH_JOB_OPTIONS`) configures retention periods and lock TTL for job handlers. Infra-layer purge handlers use `DEFAULT_BATCH_SIZE` (500) from `@d2/batch-pg` internally -- batch size is not passed via handler input.

## Handler Implementation Patterns

Concrete examples from auth handlers showing the most commonly missed patterns. These are the **mandatory** patterns — every handler must follow them.

### validateInput Pattern

Every handler defines a Zod schema and calls `this.validateInput()` at the TOP of `executeAsync`, **before** any DB/cache/gRPC calls. From `RecordSignInEvent`:

```typescript
// 1. Define Zod schema (file-level constant, not inside the class)
// Use .optional() (not .nullable() or .nullish()) for domain-aligned optional fields
const schema = z.object({
  userId: zodGuid,
  successful: z.boolean(),
  ipAddress: z.string().max(45),
  userAgent: z.string().max(512),
  whoIsId: z.string().max(64).optional(),
  deviceFingerprint: z.string().max(64).optional(),
  failureReason: z.string().max(100).optional(),
});

// 2. First lines of executeAsync — validate BEFORE any infrastructure calls
protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
  const validation = this.validateInput(schema, input);
  if (!validation.success) {
    this.context.logger.warn("RecordSignInEvent validation failed", {
      errors: validation.messages,
    });
    return D2Result.bubbleFail(validation);
  }
  // ... proceed with validated input
}
```

### RedactionSpec Pattern

Every handler touching PII declares a `RedactionSpec`. The constant lives in the **interface file** (shared with tests), and the handler overrides `get redaction()`:

```typescript
// Interface file (interfaces/cqrs/handlers/c/record-sign-in-event.ts)
export const RECORD_SIGN_IN_EVENT_REDACTION: RedactionSpec = {
  inputFields: ["ipAddress", "userAgent"],
  suppressOutput: true,
};

// Implementation file — override the getter
override get redaction() {
  return Commands.RECORD_SIGN_IN_EVENT_REDACTION;
}
```

**Important:** RedactionSpec only covers BaseHandler's automatic I/O logging. Any `this.context.logger.*` calls inside `executeAsync()` bypass it — manually review those for PII.

### Error Propagation (bubbleFail)

When calling downstream handlers (geo-client, repo handlers), **always** check the result. Never return `ok()` unconditionally. From `CreateOrgContact`:

```typescript
// Create junction record
const createResult = await this.createRecord.handleAsync({ ... });
if (!createResult.success) return D2Result.bubbleFail(createResult);

// Create Geo contact via gRPC
const geoResult = await this.createContacts.handleAsync({ contacts: [contactToCreate] });
if (!geoResult.success || !geoResult.data) {
  // Rollback: delete the junction since Geo contact creation failed
  try {
    await this.deleteRecord.handleAsync({ id: orgContactId });
  } catch {
    // Best-effort rollback
  }
  return D2Result.bubbleFail(geoResult);
}
```

**Key pattern:** Check every result → `bubbleFail` on failure → rollback side effects if needed.

## Factory Functions

Legacy factory functions (pre-DI) are still exported for backward compatibility and tests:

| Factory                            | Returns                                |
| ---------------------------------- | -------------------------------------- |
| `createSignInEventHandlers()`      | `{ record, getByUser }`                |
| `createEmulationConsentHandlers()` | `{ create, revoke, getActive }`        |
| `createOrgContactHandlers()`       | `{ create, update, delete, getByOrg }` |
| `createSignInThrottleHandlers()`   | `{ check, record }`                    |
| `createUserContactHandler()`       | `CreateUserContact` instance           |

## Dependencies

| Package           | Purpose                                            |
| ----------------- | -------------------------------------------------- |
| `@d2/auth-domain` | Domain entities, rules, constants, enums           |
| `@d2/di`          | `createServiceKey`, `ServiceCollection`            |
| `@d2/geo-client`  | Geo contact handler types, `contactInputSchema`    |
| `@d2/handler`     | `BaseHandler`, `IHandlerContext`, Zod helpers      |
| `@d2/cache-redis` | Lock key factories for job handler DI registration |
| `@d2/interfaces`  | `InMemoryCache` + `DistributedCache` handler types |
| `@d2/protos`      | `ContactDTO`, `ContactToCreateDTO` proto types     |
| `@d2/result`      | `D2Result`, `HttpStatusCode`, `ErrorCodes`         |
| `@d2/utilities`   | `generateUuidV7`                                   |
| `zod`             | Input validation schemas                           |

## Tests

All tests are in `@d2/auth-tests` (`backends/node/services/auth/tests/`):

```
src/unit/app/handlers/
  c/   record-sign-in-event.test.ts, record-sign-in-outcome.test.ts,
       create-emulation-consent.test.ts, revoke-emulation-consent.test.ts,
       create-org-contact.test.ts, update-org-contact.test.ts,
       delete-org-contact.test.ts, create-user-contact.test.ts
  q/   get-sign-in-events.test.ts, get-active-consents.test.ts,
       get-org-contacts.test.ts, check-sign-in-throttle.test.ts
src/unit/jobs/
       run-session-purge.test.ts, run-sign-in-event-purge.test.ts,
       run-invitation-cleanup.test.ts, run-emulation-consent-cleanup.test.ts
src/integration/
       job-purge-handlers.test.ts   (PurgeExpiredSessions, PurgeSignInEvents,
                                      PurgeExpiredInvitations, PurgeExpiredEmulationConsents)
```

Run: `pnpm vitest run --project auth-tests`
