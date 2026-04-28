# AUTH.md — D2 Auth Service Architecture

This document describes the complete authentication and authorization infrastructure for the D2 platform. Every rule and behavior documented here is verifiable via unit tests — no reliance on E2E.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Layer Responsibilities](#layer-responsibilities)
3. [Domain Model](#domain-model) (includes [Business Rules](#business-rules), [BetterAuth Gap Analysis](#betterauth-org-plugin--gap-analysis), [Known Upstream Bugs](#betterauth--known-upstream-bugs))
4. [Session Management (3-Tier)](#session-management-3-tier)
5. [JWT Architecture](#jwt-architecture)
6. [Fingerprint Binding (Stolen Token Detection)](#fingerprint-binding-stolen-token-detection)
7. [Authorization Framework](#authorization-framework)
8. [Middleware Pipeline](#middleware-pipeline)
9. [Fail-Closed vs Fail-Open Matrix](#fail-closed-vs-fail-open-matrix)
10. [CQRS Handlers](#cqrs-handlers)
11. [Request Flow Diagrams](#request-flow-diagrams) (includes [plain-language overview](#how-authentication-works-plain-language-overview))
12. [Email & Phone Change (OTP)](#email--phone-change-otp)
13. [Cross-Service Consistency (SAGA Pattern)](#cross-service-consistency-saga-pattern)
14. [Constants Cross-Reference](#constants-cross-reference)
15. [Security Controls Summary](#security-controls-summary)
16. [Known Security Gaps & Pre-Production Requirements](#known-security-gaps--pre-production-requirements)
17. [Secure Endpoint Construction Checklist](#secure-endpoint-construction-checklist)
18. [Test Coverage Map](#test-coverage-map)

---

## Architecture Overview

The auth service is a standalone Node.js application built on **Hono** + **BetterAuth**, following a 4-layer DDD architecture that mirrors the .NET service structure.

```
@d2/auth-domain  →  Pure domain types, rules, entities (zero deps)
@d2/auth-app     →  CQRS handlers (zero BetterAuth imports)
@d2/auth-infra   →  BetterAuth config, repos, mappers, storage adapters
@d2/auth-api     →  Hono server, middleware, routes, composition root
```

**Key constraint**: BetterAuth is an infrastructure detail. Domain types and app handlers never import from BetterAuth. All data leaving the auth boundary is domain types, never BetterAuth internals.

### Integration Points

| Consumer                | Protocol                 | Auth Mechanism                                |
| ----------------------- | ------------------------ | --------------------------------------------- |
| SvelteKit (SSR)         | HTTP proxy `/api/auth/*` | Cookie session                                |
| SvelteKit (client-side) | HTTP via proxy           | Cookie session → JWT via `authClient.token()` |
| .NET Gateway            | JWT validation via JWKS  | RS256 JWT (no BetterAuth dependency)          |
| Inter-service (gRPC)    | Proto contracts          | Bearer token (session token) or JWT           |

---

## Layer Responsibilities

### Domain (`@d2/auth-domain`)

**Who owns what**: This layer defines ALL domain types, business rules, and constants. It has zero infrastructure dependencies.

| Component     | Location                    | Purpose                                                                                                              |
| ------------- | --------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Entities      | `domain/src/entities/`      | Factory functions + types for User, Organization, Member, Invitation, SignInEvent, EmulationConsent, OrgContact      |
| Enums         | `domain/src/enums/`         | OrgType, Role, InvitationStatus — `as const` arrays + type guards                                                    |
| Rules         | `domain/src/rules/`         | emulation, membership, org-creation, invitation, sign-in-throttle (delay curve)                                      |
| Constants     | `domain/src/constants/`     | JWT_CLAIM_TYPES, SESSION_FIELDS, AUTH_POLICIES, REQUEST_HEADERS, PASSWORD_POLICY, GEO_CONTEXT_KEYS, SIGN_IN_THROTTLE |
| Value Objects | `domain/src/value-objects/` | SessionContext                                                                                                       |
| Exceptions    | `domain/src/exceptions/`    | AuthDomainError, AuthValidationError                                                                                 |

**Tested in**: `auth/tests/src/unit/domain/`

### Application (`@d2/auth-app`)

**Who owns what**: CQRS handlers for custom tables (sign_in_event, emulation_consent, org_contact). These handlers DO NOT touch BetterAuth-managed tables.

| Handler                | Type    | Purpose                                                                                                                                                 |
| ---------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RecordSignInEvent      | Command | Audit sign-in with WhoIs linkage                                                                                                                        |
| CreateEmulationConsent | Command | Grant org-level emulation consent                                                                                                                       |
| RevokeEmulationConsent | Command | Revoke active consent                                                                                                                                   |
| CreateOrgContact       | Command | Create Geo contact via gRPC + org_contact junction                                                                                                      |
| UpdateOrgContact       | Command | Update metadata (label/isPrimary) or replace contact (immutability pattern)                                                                             |
| DeleteOrgContact       | Command | Delete junction + best-effort Geo contact cleanup                                                                                                       |
| CheckSignInThrottle    | Query   | Check throttle status per (identifier, identity) — local cache → Redis, fail-open                                                                       |
| GetSignInEvents        | Query   | Paginated sign-in history (with local memory cache + staleness check)                                                                                   |
| GetActiveConsents      | Query   | User's active emulation consents                                                                                                                        |
| GetOrgContacts         | Query   | Org's contacts hydrated with full Geo contact data                                                                                                      |
| CheckEmailAvailability | Query   | Check if an email is available for registration (in-memory cache + DB, fail-open)                                                                       |
| RecordSignInOutcome    | Command | Record sign-in success/failure → mark known-good or increment failures + set lockout                                                                    |
| CreateUserContact      | Command | Create Geo contact via gRPC (`contextKey: auth_user`) during sign-up hook                                                                               |
| HandleFileProcessed    | Command | Routes file processing callbacks by contextKey: avatar → user.image, logo → org.logo                                                                    |
| RequestUserDeletion    | Command | Self-service deletion gate — password check, sole-owner check, flips status → `pending_deletion`, deletes sessions, sends "scheduled" notification      |
| CancelUserDeletion     | Command | Idempotent — re-activates a `pending_deletion` user when they sign back in (fired by BetterAuth `session.create.before` hook)                           |
| FinalizeDeletedUser    | Command | Per-user worker invoked by `CleanupDeletedUsers` job — calls AnonymizeUser, sends "complete" notification, publishes `auth.user-anonymize` fanout event |

**DI pattern**: `@d2/di` registration functions — `addAuthApp(services, options)` registers all 23 CQRS handlers (12 command + 6 query + 5 job) as transient services, `addAuthInfra(services, db)` registers all 28 repo handlers as transient services. Both accept a `ServiceCollection` and use `ServiceKey<T>` tokens for type-safe registration/resolution. Handlers resolve their dependencies (repo handlers, geo-client handlers, `IHandlerContext`) from the `ServiceProvider` at resolve time. Notification publishing uses `@d2/comms-client` (configured in `@d2/auth-api` composition root via `addCommsClient(services, { publisher })`), not app-layer handlers. See ADR-011 in `PLANNING.md`.

**Geo integration**: Org contact handlers take `@d2/geo-client` handler interfaces directly as constructor deps (`Commands.ICreateContactsHandler`, `Commands.IDeleteContactsByExtKeysHandler`, `Complex.IUpdateContactsByExtKeysHandler`, `Queries.IGetContactsByExtKeysHandler`). Contacts are accessed exclusively via ext keys (`contextKey="org_contact"`, `relatedEntityId=junction.id`). Contacts are cached locally in the geo-client's `MemoryCacheStore` (immutable, no TTL, LRU eviction). Auth-app depends on `@d2/geo-client` for handler interfaces but remains zero-gRPC (gRPC calls happen inside geo-client handlers). The geo-client is configured with `allowedContextKeys: ["auth_org_contact", "auth_user", "auth_org_invitation"]` (from `GEO_CONTEXT_KEYS`) and an `apiKey` for gRPC authentication.

**Contact immutability**: Contacts are create-only + delete. If contact info changes, the update handler calls `updateContactsByExtKeys` which atomically replaces contacts at the same ext key (Geo internally deletes old, creates new). Junction metadata (label, isPrimary) is mutable in place.

**Handler validation**: All handlers use Zod schemas via `BaseHandler.validateInput(schema, input)` at the top of `executeAsync()`. Business logic (IDOR checks, duplicate prevention, org existence) also lives in handlers, not routes.

**Tested in**: `auth/tests/src/unit/app/handlers/`

### Infrastructure (`@d2/auth-infra`)

**Who owns what**: BetterAuth configuration (Drizzle adapter), Drizzle repositories, mappers, storage adapters.

| Component           | Location                                          | Purpose                                                                                                                                  |
| ------------------- | ------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| auth-factory        | `infra/src/auth/better-auth/auth-factory.ts`      | Single BetterAuth creation point (plugins, hooks, session config, Drizzle adapter)                                                       |
| auth-config         | `infra/src/auth/better-auth/auth-config.ts`       | Config type + defaults                                                                                                                   |
| secondary-storage   | `infra/src/auth/better-auth/secondary-storage.ts` | Redis adapter wrapping @d2/interfaces cache handlers                                                                                     |
| Access Control      | `infra/src/auth/better-auth/access-control.ts`    | RBAC permission definitions                                                                                                              |
| Hooks               | `infra/src/auth/better-auth/hooks/`               | ID generation (UUIDv7), org creation validation, username auto-generation (`ensureUsername`)                                             |
| SignInThrottleStore | `infra/src/auth/sign-in-throttle-store.ts`        | Redis-backed brute-force throttle state (uses `@d2/cache-redis` handlers)                                                                |
| Drizzle Schema      | `infra/src/repository/schema/`                    | `pgTable()` declarations for all 11 tables (8 BetterAuth + 3 custom)                                                                     |
| Repo Handlers       | `infra/src/repository/handlers/{c,r,u,d}/`        | BaseHandler-based DB operations (23 handlers, TLC layout). Automatic OTel spans + D2Result error handling. PG 23505 → 409 in infra layer |
| Repo Factories      | `infra/src/repository/handlers/factories.ts`      | `createSignInEventRepoHandlers(db, ctx)`, `createEmulationConsentRepoHandlers(db, ctx)`, `createOrgContactRepoHandlers(db, ctx)`         |
| Migrations          | `infra/src/repository/migrations/`                | Drizzle auto-generated SQL migrations (all tables + indexes)                                                                             |
| Migration runner    | `infra/src/repository/migrate.ts`                 | Programmatic `runMigrations(pool)` for app startup + tests                                                                               |
| Mappers             | `infra/src/mappers/`                              | BetterAuth record → domain entity conversion                                                                                             |

**BetterAuth plugins** (configured in auth-factory):

| Plugin         | Purpose                                                               |
| -------------- | --------------------------------------------------------------------- |
| `bearer`       | Session token via Authorization header (for non-cookie clients)       |
| `jwt`          | RS256 JWT issuance with custom `definePayload`                        |
| `username`     | Username-based sign-in (`/sign-in/username`) + username field on user |
| `organization` | Multi-org support (roles, invitations, creation rules)                |
| `admin`        | Impersonation support (1-hour session)                                |

**Email verification** (configured in auth-factory, not a plugin):

| Setting                       | Value   | Purpose                                                |
| ----------------------------- | ------- | ------------------------------------------------------ |
| `requireEmailVerification`    | `true`  | Blocks sign-in for unverified emails                   |
| `autoSignIn`                  | `false` | No session created at sign-up                          |
| `sendOnSignUp`                | `true`  | Sends verification email on sign-up                    |
| `sendOnSignIn`                | `true`  | Re-sends verification email on blocked sign-in attempt |
| `autoSignInAfterVerification` | `true`  | Creates session after email verification               |
| `sendVerificationEmail`       | Wired   | Publishes notification via `@d2/comms-client`          |

**Tested in**: `auth/tests/src/unit/infra/`

### API (`@d2/auth-api`)

**Who owns what**: HTTP layer — Hono app, middleware pipeline, route handlers, composition root, DI scoping.

| Component        | Location                          | Purpose                                                                                                                                                     |
| ---------------- | --------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Composition root | `api/src/composition-root.ts`     | DI assembly via `ServiceCollection` + `ServiceProvider`                                                                                                     |
| App setup        | `api/src/setup/hono-app-setup.ts` | Hono app builder with full middleware pipeline and route composition                                                                                        |
| Scope middleware | `api/src/middleware/scope.ts`     | Per-request DI scope on protected routes                                                                                                                    |
| Middleware       | `api/src/middleware/`             | Thin Hono adapters delegating to shared packages (@d2/csrf, @d2/service-key, @d2/session-fingerprint), plus session, enrichment, rate-limit, error handling |
| Routes           | `api/src/routes/`                 | Thin routes with visible authorization (5-8 lines each)                                                                                                     |
| gRPC services    | `api/src/services/`               | AuthServiceServer (health) + auth-jobs gRPC service + FileCallbackService (OnFileProcessed + CanAccess)                                                     |

**Shared middleware delegation**: CSRF (`@d2/csrf`), session fingerprint (`@d2/session-fingerprint`), service key (`@d2/service-key`), and translation (`@d2/translation`) middleware delegate to shared packages under `backends/node/shared/implementations/middleware/` for framework-agnostic core logic. Auth API middleware files are thin Hono adapters around these shared packages. The error handler uses `TK` translation key constants (from `@d2/i18n`) instead of hardcoded English strings. The auth service also uses `@d2/i18n` (`BASE_LOCALE`, `createTranslator`) for locale resolution — `BASE_LOCALE` defaults to `"en-US"` (BCP 47 tag, configurable via `PUBLIC_DEFAULT_LOCALE` env var).

**Thin routes pattern**: Route handlers are intentionally thin — they extract input from the request (body, session, query params), call the handler, and return the result. All validation, business logic, and IDOR checks live in app-layer handlers. Authorization is declared via middleware at route registration (`requireOrg()`, `requireStaff()`, `requireRole("officer")`), making security requirements visible at a glance.

#### Dependency Injection Architecture

The auth service uses `@d2/di` (`ServiceCollection` / `ServiceProvider` / `ServiceScope`) mirroring .NET's DI pattern. See ADR-011 in `PLANNING.md` for full rationale.

**Composition root** (`api/src/composition-root.ts`):

1. Create singletons: `pg.Pool`, Redis, logger
2. Run Drizzle migrations + create Drizzle instance
3. Register all services in `ServiceCollection` via `addAuthInfra(services, db)`, `addAuthApp(services, options)`, and `addCommsClient(services, { publisher })`
4. Build immutable `ServiceProvider`
5. Create pre-auth singletons (FindWhoIs, RateLimit, Throttle, CheckEmailAvailability) — these remain outside DI with service-level context
6. Create pre-auth password functions (HIBP + blocklist)
7. Create i18n translator (`@d2/i18n` — loads `contracts/messages/*.json` at startup)
8. Create BetterAuth with scoped callbacks (including `createUserContact` with locale)
9. Build Hono app with scope middleware on protected routes
10. Build gRPC server (AuthServiceServer for health checks, auth-jobs service, FileCallbackService for file processing callbacks from Files service)

**Service lifetimes:**

| Category                             | Lifetime  | Why                                                                     |
| ------------------------------------ | --------- | ----------------------------------------------------------------------- |
| Logger, Redis, geo-client handlers   | Singleton | Shared infrastructure — one instance for application lifetime           |
| `IHandlerContext`, `IRequestContext` | Scoped    | Per-request user/trace context — new per DI scope                       |
| All CQRS handlers, repo handlers     | Transient | New instance per resolve — receive scoped `IHandlerContext` via factory |
| Sign-in throttle store               | Singleton | Redis-backed state — shared across all requests                         |

**Pre-auth singletons** (FindWhoIs, CheckRateLimit, CheckSignInThrottle, RecordSignInOutcome, CheckEmailAvailability): These handlers execute before authentication and are NOT registered in the DI container. They are constructed once at startup with a service-level `HandlerContext` containing static anonymous defaults. However, they **automatically see per-request context** via ambient `AsyncLocalStorage` — `HandlerContext` checks `requestContextStorage` / `requestLoggerStorage` first, falling back to constructor args only when no ambient storage is active. This mirrors .NET's DI scoping behavior where all handlers get per-request `IRequestContext` from `HttpContext.Features`.

**Ambient context pipeline**: The `createAmbientScopeMiddleware()` wraps the request pipeline in `AsyncLocalStorage.run()`, seeded with the enrichment-populated `IRequestContext` and per-request logger. After auth, `createScopeMiddleware()` upgrades the ambient context via `.enterWith()` to include identity/org fields. Pre-auth singletons see enrichment data (IP, fingerprints, trust flag); post-auth handlers see the full auth-enriched context.

**Scoping patterns:**

- **Protected routes**: `createScopeMiddleware(provider)` creates a `ServiceScope` per request, builds `IRequestContext` from session data (user ID, org context, emulation state), sets it on the scope alongside a fresh `IHandlerContext`. Routes resolve handlers via `c.get("scope").resolve(key)`. Scope is disposed after the request completes
- **BetterAuth callbacks**: `createCallbackScope()` creates a temporary scope with an anonymous `IRequestContext` (no authenticated user) for handlers that fire during BetterAuth processing (e.g., `RecordSignInEvent` on sign-in, `CreateUserContact` on sign-up, comms-client notifications). Each callback gets its own scope with a unique traceId, disposed in a `try/finally` block

**Tested in**: `auth/tests/src/unit/api/`

---

## Domain Model

### BetterAuth-Managed Tables

These tables are owned and managed by BetterAuth via the Drizzle adapter. Their schema is declared in `infra/src/repository/schema/better-auth-tables.ts` and included in the Drizzle migration alongside custom tables. BetterAuth reads/writes through the Drizzle adapter — we do NOT use BetterAuth's internal Kysely adapter.

| Table          | Key Fields                                                               | Notes                                                                                                                                 |
| -------------- | ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------- |
| `user`         | id, email, name, username, displayUsername, image, emailVerified, locale | UUIDv7 IDs, username auto-generated by `ensureUsername` hook. `locale` custom field defaults to `BASE_LOCALE` (`"en-US"`, BCP 47 tag) |
| `account`      | id, userId, providerId, accountId                                        | 1:N user:account (credential, Google, GitHub, etc.)                                                                                   |
| `session`      | id, userId, expiresAt, ipAddress, userAgent + 4 custom fields            | 3-tier storage                                                                                                                        |
| `verification` | id, identifier, value, expiresAt                                         | Email verification/password reset tokens                                                                                              |
| `jwks`         | id, publicKey, privateKey, createdAt                                     | RS256 key pairs for JWT signing                                                                                                       |
| `organization` | id, name, slug, logo, metadata + orgType                                 | Custom `orgType` field                                                                                                                |
| `member`       | id, organizationId, userId, role                                         | Standard BetterAuth membership                                                                                                        |
| `invitation`   | id, organizationId, email, role, status, expiresAt                       | With state machine transitions                                                                                                        |

### Custom Tables (Drizzle Migrations)

These tables are managed by Drizzle schema declarations in `infra/src/repository/schema/custom-tables.ts`, with auto-generated SQL migrations in `infra/src/repository/migrations/`.

| Table               | Purpose                             | Key Fields                                                                                     |
| ------------------- | ----------------------------------- | ---------------------------------------------------------------------------------------------- |
| `sign_in_event`     | Audit trail for sign-in attempts    | userId, successful, ipAddress, userAgent, whoIsId                                              |
| `emulation_consent` | Per-user, per-org emulation consent | userId, grantedToOrgId, expiresAt, revokedAt                                                   |
| `org_contact`       | Org ↔ Geo Contact junction          | organizationId, label, isPrimary (no contactId — junction ID is the ext key `relatedEntityId`) |

**Partial unique index**: `emulation_consent` has a unique index on `(user_id, granted_to_org_id)` WHERE `revoked_at IS NULL` — prevents duplicate active consents.

### Session Extension Fields

BetterAuth sessions carry 5 custom fields (configured via `session.additionalFields`). Session `ipAddress`/`userAgent` are `?: string` (optional data). Auth-state fields remain `| null` (three-state pre-auth semantics: `null` = "not yet determined", absent = never set):

| Field                      | Constant                           | Type    | Purpose                              |
| -------------------------- | ---------------------------------- | ------- | ------------------------------------ |
| `activeOrganizationType`   | `SESSION_FIELDS.ACTIVE_ORG_TYPE`   | string? | Current org type (set on org switch) |
| `activeOrganizationRole`   | `SESSION_FIELDS.ACTIVE_ORG_ROLE`   | string? | Current role in active org           |
| `activeOrganizationId`     | `SESSION_FIELDS.ACTIVE_ORG_ID`     | string? | Current org ID                       |
| `emulatedOrganizationId`   | `SESSION_FIELDS.EMULATED_ORG_ID`   | string? | Emulated org (if emulating)          |
| `emulatedOrganizationType` | `SESSION_FIELDS.EMULATED_ORG_TYPE` | string? | Emulated org type                    |

**Session enrichment**: BetterAuth's `setActiveOrganization` only sets `activeOrganizationId` natively. The `databaseHooks.session.update.before` hook in `auth-factory.ts` enriches the session patch with `activeOrganizationType` (from org table) and `activeOrganizationRole` (from member table) **before** BetterAuth writes to DB/Redis/cookie — eliminating any staleness window. Clearing the active org (`organizationId: null`) also clears both custom fields. All org-activation triggers (explicit setActive, org creation auto-activate, invitation acceptance, org deletion) flow through this hook.

**Verified by**: `auth/tests/src/unit/infra/mappers/session-mapper.test.ts`, `auth/tests/src/integration/better-auth-behavior.test.ts`

### Organization Types

5 flat types (no hierarchy, no parent-child):

| Type          | Constant                    | Self-Creation              | Notes                   |
| ------------- | --------------------------- | -------------------------- | ----------------------- |
| `admin`       | `OrgTypeValues.ADMIN`       | No (admin-only)            | Platform administrators |
| `support`     | `OrgTypeValues.SUPPORT`     | No (admin-only)            | Support staff           |
| `customer`    | `OrgTypeValues.CUSTOMER`    | Yes (self-service)         | Primary business users  |
| `third_party` | `OrgTypeValues.THIRD_PARTY` | No (customer via workflow) | External partners       |
| `affiliate`   | `OrgTypeValues.AFFILIATE`   | No (admin-only)            | Affiliate partners      |

**"Staff"** = `admin` or `support` (used in authorization policies).

**Verified by**: `auth/tests/src/unit/domain/enums/org-type.test.ts`, `auth/tests/src/unit/domain/rules/org-creation.test.ts`

### Role Hierarchy

> **Two separate "role" concepts exist — don't confuse them:**
>
> | Concept             | Where                                            | Purpose                                                                                                                              | Default                                           |
> | ------------------- | ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------- |
> | **User-level role** | `user.role` column                               | BetterAuth admin plugin internal field. **Not exposed to frontend** — `AuthUser` type excludes it, `SessionResolver` doesn't map it. | `"agent"` (via `admin({ defaultRole: "agent" })`) |
> | **Org-level role**  | `member.role` / `session.activeOrganizationRole` | Actual authorization role used by the app for access control. Set when user joins an org.                                            | Set per membership                                |
>
> The user-level `role` appears in raw BetterAuth API responses (e.g., `authClient.getSession()`) but is **never** consumed by D2-WORX application code. It exists solely because the `admin()` plugin requires a `defaultRole` for impersonation features.

Roles are hierarchical — higher index = more privileges:

```
auditor (0) < agent (1) < officer (2) < owner (3)
```

| Role      | Level | Key Permissions                       |
| --------- | ----- | ------------------------------------- |
| `auditor` | 0     | Read-only (businessData, orgSettings) |
| `agent`   | 1     | + Create/update businessData          |
| `officer` | 2     | + Delete + billing + member CRU       |
| `owner`   | 3     | + Full org settings + member CRUD     |

`AtOrAbove(minRole)` returns all roles at or above the minimum (used in policy evaluation).

Example: `AtOrAbove("officer")` → `["officer", "owner"]`

**Verified by**: `auth/tests/src/unit/domain/enums/role.test.ts`, `.NET: AuthPolicyTests.cs`

### Business Rules

#### Onboarding Flow

1. User signs up (email/password or Google/LinkedIn)
2. Email verification required (`requireEmailVerification: true`)
3. Post-verification screen: accept pending invitation(s) OR create a `customer` org
4. No app access until at least one org membership exists
5. SvelteKit redirects unauthenticated users to `(auth)/`, no-org users to `(onboarding)/`

#### No-Org State

- Freshly signed-up users have no `activeOrganizationId`
- SvelteKit redirects to onboarding flow: accept pending invites or create an org
- No access to any app features until at least one org membership exists

#### Member Removal Cascading

- When a user is removed from an org, all their sessions with that org as `activeOrganizationId` are **immediately terminated**
- SignalR gateway signs them out in real-time (WebSocket disconnect)
- If removed from their only org, user returns to onboarding state (join/create)
- **Last-owner protection**: The last `owner` is blocked from leaving or downgrading. Two options presented:
  1. **Transfer ownership** — select another member → email confirmation → ownership transferred → original owner can then leave
  2. **Delete the org** — email confirmation required → org and all associated data deleted

#### Invitation Lifecycle

- Invitations expire after **7 days**
- Org owners can revoke pending invitations
- Users can **accept** or **reject** invitations
- Expired invitations are cleaned up by scheduled Dkron job (daily, 7-day grace period)

#### Account Linking

- One account per provider per user (no duplicate Google accounts, etc.)
- BetterAuth's `accountLinking.trustedProviders` controls which OAuth providers auto-link on matching email

#### Sign-In Event Retention

- 90-day retention (matches WhoIs retention pattern)
- Purge via scheduled Dkron job (`auth-purge-sign-in-events`, daily at 02:45 UTC)

### BetterAuth Org Plugin — Gap Analysis

75% OOTB fit. Gaps are bridgeable with hooks and custom fields:

| Requirement                     | OOTB? | Gap                                    | Effort |
| ------------------------------- | ----- | -------------------------------------- | ------ |
| Users belong to 0+ orgs         | Yes   | None                                   | —      |
| Sign up, join/create orgs later | Yes   | None                                   | —      |
| 5 organization types            | No    | Add custom field + validation hook     | Low    |
| Custom roles (4 levels)         | Yes   | No hierarchy syntax, compose manually  | Low    |
| Org-specific session switching  | Yes   | Known bugs (#4708, #3233)              | Low    |
| Org type in session             | No    | Custom session fields                  | Low    |
| Org emulation                   | No    | Custom session fields + middleware     | Medium |
| User impersonation              | Yes   | Built-in `impersonation` plugin        | —      |
| Org context in JWT              | No    | `definePayload` needs session lookup   | Medium |
| Invitation per org type         | No    | Branch logic in hooks                  | Low    |
| Admin cross-org visibility      | No    | Query DB directly                      | Medium |
| Member list privacy per role    | No    | Security issue #6038, need hook filter | Medium |

### BetterAuth — Known Upstream Bugs

Tracked BetterAuth issues that may affect our implementation. Monitor for fixes in new releases.

**HIGH — Session + Secondary Storage:**

| Issue                                                           | Description                                                                    | Mitigation                                                            |
| --------------------------------------------------------------- | ------------------------------------------------------------------------------ | --------------------------------------------------------------------- |
| [#6987](https://github.com/better-auth/better-auth/issues/6987) | `updateSession` doesn't sync back to Redis (stale data)                        | Monitor; may need to manually invalidate Redis on profile update      |
| [#6993](https://github.com/better-auth/better-auth/issues/6993) | Session in Redis missing `id` field with `storeSessionInDatabase: true`        | Test early; may be fixed in newer versions                            |
| [#5144](https://github.com/better-auth/better-auth/issues/5144) | `revokeSession` removes from Redis but not PG with `preserveSessionInDatabase` | Don't use `preserveSessionInDatabase` initially; test revocation flow |
| [#4203](https://github.com/better-auth/better-auth/issues/4203) | Redis TTL expiry doesn't fall back to DB (premature invalidation)              | Set Redis TTL >= session `expiresIn` to avoid premature expiry        |
| [#3819](https://github.com/better-auth/better-auth/issues/3819) | `active-sessions` list not cleaned on sign-out                                 | Test `listSessions()` after sign-out; may need workaround             |

**MODERATE — Schema/Casing:**

| Issue                                                           | Description                                                  | Mitigation                                                    |
| --------------------------------------------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------- |
| [#5649](https://github.com/better-auth/better-auth/issues/5649) | SSO/OIDC plugins bypass `casing: "snake_case"` field mapping | We don't use SSO/OIDC initially; test if added later          |
| [#3774](https://github.com/better-auth/better-auth/issues/3774) | `modelName` hardcoded in some internal paths                 | Test custom table names with our plugin combination           |
| [#3954](https://github.com/better-auth/better-auth/issues/3954) | JWKS table queried on every `getSession()` (not cached)      | Performance concern; monitor and potentially cache externally |

**MODERATE — Organization Plugin:**

| Issue                                                           | Description                                                   | Mitigation                                         |
| --------------------------------------------------------------- | ------------------------------------------------------------- | -------------------------------------------------- |
| [#4708](https://github.com/better-auth/better-auth/issues/4708) | `set-active` endpoint sometimes has null session context      | Use `getSessionFromCtx` workaround                 |
| [#6038](https://github.com/better-auth/better-auth/issues/6038) | `/get-full-organization` exposes member list to all roles     | Hook-based filter or endpoint wrapper              |
| [#6081](https://github.com/better-auth/better-auth/issues/6081) | `hasPermission` silently returns false for unknown roles      | Fix in progress (PR #6097); add defensive logging  |
| [#2100](https://github.com/better-auth/better-auth/issues/2100) | `updateMemberRole` fails on members with existing multi-roles | Avoid multi-role per member initially; monitor fix |

**LOW — Design Considerations:**

- `customSession` plugin data is NOT cached in Redis or cookie cache (function runs every `getSession()`)
- BetterAuth's built-in rate limiter is per-path only — we use our own `@d2/ratelimit` instead
- Cookie cache invalidation is version-based (`cookieCache.version`), not per-user
- No built-in role hierarchy — must compose permissions manually (spread pattern)
- `definePayload` receives user, not session — need session lookup for org context in JWT

---

## Session Management (3-Tier)

BetterAuth is session-based at its core. Sessions use 3-tier storage for performance and durability:

| Tier              | Storage          | Lookup Cost                 | Revocation Lag      | Configured Via                 |
| ----------------- | ---------------- | --------------------------- | ------------------- | ------------------------------ |
| Cookie cache      | Encrypted cookie | Zero (travels with request) | Up to 5min (maxAge) | `cookieCache.maxAge`           |
| Secondary storage | Redis            | ~1ms                        | Instant             | `secondaryStorage` adapter     |
| Primary DB        | PostgreSQL       | ~5-10ms                     | Instant             | `storeSessionInDatabase: true` |

### Configuration Defaults

| Setting               | Value            | Constant                                 |
| --------------------- | ---------------- | ---------------------------------------- |
| Session expiry        | 7 days (604800s) | `AUTH_CONFIG_DEFAULTS.sessionExpiresIn`  |
| Session update age    | 1 day (86400s)   | `AUTH_CONFIG_DEFAULTS.sessionUpdateAge`  |
| Cookie cache maxAge   | 5 minutes (300s) | `AUTH_CONFIG_DEFAULTS.cookieCacheMaxAge` |
| Cookie cache strategy | `"compact"`      | —                                        |

### Session Lifecycle

1. **Sign-up**: User created with `emailVerified: false`. No session created. Verification email sent via hook
1. **Email verification**: User clicks verification link → `emailVerified` set to `true` → auto-sign-in creates session
1. **Sign-in**: BetterAuth creates session → dual-writes to Redis + PostgreSQL → sets cookie
1. **Subsequent requests**: Cookie cache hit (0ms) → Redis lookup (1ms) → PG fallback (5-10ms)
1. **Org switch**: Session updated with new org context fields → Redis + PG
1. **Revocation**: `revokeSession` deletes from Redis + PG. Cookie cache expires naturally in ≤5min
1. **Expiry**: Session TTL enforced at all 3 tiers

### Locale & Timezone Hydration on Sign-Up

SvelteKit sets two browser-derived cookies on every page load (defaults to whatever the browser reports — no user action required):

| Cookie        | Source                                             | Purpose                                            |
| ------------- | -------------------------------------------------- | -------------------------------------------------- |
| `D2_LOCALE`   | Negotiated locale (Paraglide / `Accept-Language`)  | UI language preference                             |
| `D2_TIMEZONE` | `Intl.DateTimeFormat().resolvedOptions().timeZone` | IANA timezone identifier (e.g., `America/Toronto`) |

The BetterAuth sign-up callback reads BOTH cookies off the inbound request and persists them onto the new `user` row (`user.locale`, `user.timezone`). New users land in the system with their browser's locale and timezone preserved — no post-sign-up onboarding step needed for either field. After sign-up, the profile page dropdowns let the user change either independently via `UpdateUserLocale` / `UpdateUserTimezone` (both follow the SAGA pattern — see [Cross-Service Consistency](#cross-service-consistency-saga-pattern)).

### Cookie Signing & Token Types

BetterAuth distinguishes between **raw session tokens** and **signed cookie values**. This is critical for understanding how different auth paths work.

#### Two cookies are set on sign-in

| Cookie                      | Format                     | TTL    | Purpose                                         |
| --------------------------- | -------------------------- | ------ | ----------------------------------------------- |
| `better-auth.session_token` | `TOKEN.SIGNATURE`          | 7 days | Signed session token — used for DB/Redis lookup |
| `better-auth.session_data`  | Base64(JSON session + sig) | 5 min  | Cookie cache — skips DB/Redis when fresh        |

#### Raw token vs signed cookie value

| Source                                 | Format              | Use with                                            |
| -------------------------------------- | ------------------- | --------------------------------------------------- |
| `auth.api.signInEmail()` → `res.token` | Raw token (`TOKEN`) | `Authorization: Bearer TOKEN`                       |
| HTTP sign-in `set-cookie` header       | `TOKEN.SIGNATURE`   | `Cookie: better-auth.session_token=TOKEN.SIGNATURE` |
| Database `session.token` column        | Raw token (`TOKEN`) | Internal lookups only                               |

The **raw token** from the in-process API is the same value stored in the `session` table. It works with the **Bearer plugin** (`Authorization: Bearer TOKEN`), which looks up the session by the raw token.

The **signed cookie value** includes a signature appended after a `.` delimiter. BetterAuth validates this signature when parsing cookies. If the signature is missing or invalid, BetterAuth **silently returns `null`** (200 status with null body) — it does NOT return 401 or an error.

#### Silent null on invalid cookies

When `GET /api/auth/get-session` receives a cookie with an unsigned or tampered value, BetterAuth returns `200` with `null` body. This is indistinguishable from "no session" at the HTTP level. The `SessionResolver` in `@d2/auth-bff-client` detects this case (session cookie present but null response) and logs a warning for monitoring visibility.

**Implications:**

- Legitimate clients always have signed cookies (set by BetterAuth via `set-cookie` headers)
- Attackers sending unsigned/tampered cookies are correctly rejected (treated as unauthenticated)
- The `signInEmail` response shape is `{ redirect, token, url, user }` — there is no `session` property
- E2E tests that need cookie-based auth must sign in via HTTP and extract cookies from the `set-cookie` response header — they CANNOT construct cookies from raw tokens

### Secondary Storage Adapter

The `createSecondaryStorage` function wraps `@d2/interfaces` distributed cache handlers behind BetterAuth's `SecondaryStorage` contract:

```typescript
interface SecondaryStorage {
  get(key: string): Promise<string | null>;
  set(key: string, value: string, ttl?: number): Promise<void>;
  delete(key: string): Promise<void>;
}
```

**No direct ioredis dependency** — the composition root injects pre-configured cache handlers. This allows the same Redis connection pool to be reused for caching and session storage.

**Verified by**: `auth/tests/src/unit/infra/secondary-storage.test.ts`

### Cache Invalidation Strategy

Auth uses **intentional TTL-based expiry** rather than explicit write-through invalidation. Each cache layer has its own staleness guarantees:

| Layer                     | Invalidation Method                                                                | Max Staleness |
| ------------------------- | ---------------------------------------------------------------------------------- | ------------- |
| Session cookie cache      | `cookieCache.maxAge` (5 min) — browser sends stale cookie, BetterAuth re-validates | 5 minutes     |
| Session Redis (secondary) | Explicit delete on `revokeSession`; TTL set on write                               | Instant       |
| Session PostgreSQL        | Dual-write — always current                                                        | Instant       |
| Sign-in throttle          | Per-key TTLs via Redis SET + EX                                                    | Per-TTL       |
| Sign-in events query      | Staleness check: compare latest event date for user — if unchanged, return cached  | 0 (validated) |

This is a deliberate design choice: auth data changes infrequently (sign-ins, session revocations), so short TTLs provide sufficient consistency without the complexity of cross-instance cache invalidation.

**Cross-ref**: `secondary-storage.ts` (session cache), `sign-in-throttle-store.ts` (throttle TTLs)

---

## JWT Architecture

JWTs are issued for **service-to-service calls and .NET gateway authentication**. Browser-to-SvelteKit uses cookie sessions (not JWTs).

### Token Configuration

| Setting       | Value                                        | Notes                                       |
| ------------- | -------------------------------------------- | ------------------------------------------- |
| Algorithm     | RS256                                        | Native .NET support — NO EdDSA              |
| Expiration    | 15 minutes                                   | `AUTH_CONFIG_DEFAULTS.jwtExpirationSeconds` |
| JWKS endpoint | `/api/auth/.well-known/openid-configuration` | Standard OIDC discovery                     |
| Key rotation  | 30 days                                      | `AUTH_CONFIG_DEFAULTS.jwksRotationDays`     |
| Grace period  | 30 days                                      | Old keys still valid during rotation        |
| Key size      | 2048-bit RSA                                 | `modulusLength: 2048`                       |

### JWT Payload (definePayload)

The `definePayload` function in `auth-factory.ts` constructs the JWT payload from user + session data:

| Claim                   | Constant                                 | Source                                    | Type            |
| ----------------------- | ---------------------------------------- | ----------------------------------------- | --------------- |
| `sub`                   | `JWT_CLAIM_TYPES.SUB`                    | `user.id`                                 | string (UUIDv7) |
| `email`                 | `JWT_CLAIM_TYPES.EMAIL`                  | `user.email`                              | string          |
| `username`              | `JWT_CLAIM_TYPES.USERNAME`               | `user.username` (auto-generated)          | string          |
| `orgId`                 | `JWT_CLAIM_TYPES.ORG_ID`                 | `session[ACTIVE_ORG_ID]`                  | string?         |
| `orgType`               | `JWT_CLAIM_TYPES.ORG_TYPE`               | `session[ACTIVE_ORG_TYPE]`                | string?         |
| `role`                  | `JWT_CLAIM_TYPES.ROLE`                   | `session[ACTIVE_ORG_ROLE]`                | string?         |
| `emulatedOrgId`         | `JWT_CLAIM_TYPES.EMULATED_ORG_ID`        | `session[EMULATED_ORG_ID]`                | string?         |
| `isEmulating`           | `JWT_CLAIM_TYPES.IS_EMULATING`           | Derived from emulatedOrgId                | boolean         |
| `impersonatedBy`        | `JWT_CLAIM_TYPES.IMPERSONATED_BY`        | `session["impersonatedBy"]`               | string?         |
| `isImpersonating`       | `JWT_CLAIM_TYPES.IS_IMPERSONATING`       | Derived from impersonatedBy               | boolean         |
| `impersonatingEmail`    | `JWT_CLAIM_TYPES.IMPERSONATING_EMAIL`    | DB lookup on impersonator user            | string?         |
| `impersonatingUsername` | `JWT_CLAIM_TYPES.IMPERSONATING_USERNAME` | DB lookup on impersonator user            | string?         |
| `fp`                    | `JWT_CLAIM_TYPES.FINGERPRINT`            | `hooks.getFingerprintForCurrentRequest()` | string?         |

> **Note**: The constants `JWT_CLAIM_TYPES.ORG_NAME`, `EMULATED_ORG_NAME`, and `EMULATED_ORG_TYPE` are defined in `auth-constants.ts` but are NOT currently emitted by `definePayload`. They exist for future use when org name/emulated org context is added to JWT claims.

### .NET Gateway JWT Validation

The .NET gateway validates JWTs without any BetterAuth dependency:

1. **JWKS retrieval**: Automatic from `MetadataAddress` (OIDC discovery endpoint)
2. **Issuer validation**: Must match `JwtAuthOptions.Issuer`
3. **Audience validation**: Must match `JwtAuthOptions.Audience`
4. **Lifetime validation**: Token must not be expired (30s clock skew)
5. **Algorithm restriction**: Only RS256 accepted (`ValidAlgorithms = ["RS256"]`)
6. **Signed token requirement**: `RequireSignedTokens = true`
7. **Fingerprint validation**: Post-auth middleware compares `fp` claim vs computed hash

**Verified by**: `.NET: JwtFingerprintMiddlewareTests.cs`, `.NET: JwtFingerprintValidatorTests.cs`

### Org Emulation vs User Impersonation in JWT

Two distinct security concepts with different JWT shapes:

**Org Emulation** (staff viewing another org's data read-only):

| Claim           | Value                                          |
| --------------- | ---------------------------------------------- |
| `sub`           | Staff user's ID                                |
| `orgId`         | Staff's own org ID                             |
| `emulatedOrgId` | Target org's ID                                |
| `isEmulating`   | `true`                                         |
| `role`          | Forced to `auditor` by `resolveSessionContext` |

**User Impersonation** (admin acting as another user):

| Claim             | Value                   |
| ----------------- | ----------------------- |
| `sub`             | Impersonated user's ID  |
| `orgId`           | Impersonated user's org |
| `impersonatedBy`  | Admin user's ID         |
| `isImpersonating` | `true`                  |

**Verified by**: `auth/tests/src/unit/domain/rules/emulation.test.ts`, `.NET: RequestContextJwtTests.cs`

---

## Fingerprint Binding (Stolen Token Detection)

Two independent fingerprint checks prevent token theft:

### 1. Session Fingerprint (Node.js — @d2/session-fingerprint)

**Where**: Core logic in `@d2/session-fingerprint` shared package (`backends/node/shared/implementations/middleware/session-fingerprint/default/`). Auth API's `middleware/session-fingerprint.ts` is a thin Hono adapter.

**Formula**: `SHA-256(User-Agent + "|" + Accept)`

**Behavior**:

1. On session creation → compute fingerprint, store in Redis (`fp:{sessionId}`)
2. On subsequent requests → recompute, compare with stored value
3. **Match** → pass through
4. **Mismatch** → auto-revoke session, return 401

**Storage**: Redis with TTL matching session lifetime.

**Verified by**: `auth/tests/src/unit/api/middleware/session-fingerprint.test.ts`

### 2. JWT Fingerprint (.NET Gateway)

**Where**: `JwtAuth.Default/JwtFingerprintMiddleware.cs` + `JwtAuth.Default/JwtFingerprintValidator.cs` (in `backends/dotnet/shared/Implementations/Middleware/JwtAuth.Default/`)

**Formula**: `SHA-256(User-Agent + "|" + Accept)` (identical to Node.js)

**Behavior**:

1. JWT's `fp` claim contains hash computed at token issuance time
2. Gateway recomputes hash from current request headers
3. **Match** → pass through
4. **Mismatch** → 401 Unauthorized (D2Result error body)
5. **No fp claim (non-trusted)** → 401 Unauthorized (`MISSING_FINGERPRINT` — fingerprint is required)
6. **Trusted service** (`IsTrustedService` flag) → skip fingerprint validation entirely
7. **Not authenticated** → pass through (auth middleware handles)

**Cross-platform parity**: Both Node.js and .NET use the same `SHA-256(UA|Accept)` formula, ensuring the `fp` claim computed at JWT issuance matches what the .NET gateway validates.

**Verified by**: `.NET: JwtFingerprintMiddlewareTests.cs`, `.NET: JwtFingerprintValidatorTests.cs`, `auth/tests/src/unit/api/middleware/jwt-fingerprint.test.ts`

---

## Authorization Framework

### Named Policies (Shared .NET + Node.js)

| Policy          | Constant                       | Requirement                                 |
| --------------- | ------------------------------ | ------------------------------------------- |
| `Authenticated` | `AUTH_POLICIES.AUTHENTICATED`  | Valid JWT / session (any user)              |
| `HasActiveOrg`  | `AUTH_POLICIES.HAS_ACTIVE_ORG` | `orgId` + `orgType` + `role` claims present |
| `StaffOnly`     | `AUTH_POLICIES.STAFF_ONLY`     | `orgType` ∈ {admin, support}                |
| `AdminOnly`     | `AUTH_POLICIES.ADMIN_ONLY`     | `orgType` = admin                           |

### .NET Policy Registration

Policies are registered via `AddD2Policies()` extension method on `AuthorizationOptions`:

```csharp
// Standard policies
services.AddAuthorization(o => o.AddD2Policies());

// Custom composite policies
options.RequireOrgType("CustomersOnly", OrgTypeValues.CUSTOMER, OrgTypeValues.THIRD_PARTY);
options.RequireRole("OfficerPlus", RoleValues.OFFICER);
options.RequireOrgTypeAndRole("StaffOfficer", OrgTypeValues.STAFF, RoleValues.OFFICER);
```

**Verified by**: `.NET: AuthPolicyTests.cs` (14 tests)

### Node.js Authorization Middleware (Hono)

Composable middleware factories that read from `c.var.session`:

| Middleware                 | Checks                                                  | Rejects With                    |
| -------------------------- | ------------------------------------------------------- | ------------------------------- |
| `requireOrg()`             | Session has activeOrgId + valid orgType + valid role    | 403 (no org) / 401 (no session) |
| `requireOrgType(...types)` | `activeOrganizationType` ∈ allowed set                  | 403                             |
| `requireRole(minRole)`     | `ROLE_HIERARCHY[activeRole] >= ROLE_HIERARCHY[minRole]` | 403                             |
| `requireStaff()`           | Shorthand: `requireOrgType("admin", "support")`         | 403                             |
| `requireAdmin()`           | Shorthand: `requireOrgType("admin")`                    | 403                             |

Composition example:

```typescript
app.use("/api/emulation/*", requireOrg(), requireStaff(), requireRole("officer"));
```

**Verified by**: `auth/tests/src/unit/api/middleware/authorization.test.ts` (23 tests)

### Role Hierarchy in Policy Evaluation

`requireRole("officer")` accepts officers AND owners. The `AtOrAbove` function returns:

| Input       | Result                                     |
| ----------- | ------------------------------------------ |
| `"auditor"` | `["auditor", "agent", "officer", "owner"]` |
| `"agent"`   | `["agent", "officer", "owner"]`            |
| `"officer"` | `["officer", "owner"]`                     |
| `"owner"`   | `["owner"]`                                |
| `"invalid"` | `[]`                                       |

**Verified by**: `.NET: AuthPolicyTests.cs` (AtOrAbove tests), `auth/tests/src/unit/domain/enums/role.test.ts`

---

## Middleware Pipeline

### Node.js (Hono) Middleware Order

The composition root builds the pipeline in this exact order:

```
1.  CORS (Hono built-in)
2.  Security headers (X-Content-Type-Options: nosniff, X-Frame-Options: DENY)
3.  Body limit (256KB)
4.  Request enrichment (IP resolution, fingerprinting, WhoIs lookup)
5.  Service key detection (@d2/service-key — X-Api-Key → sets IsTrustedService; require: true → 401 if missing)
6.  Request context logging (per-request child logger with network bindings)
7.  Ambient scope (AsyncLocalStorage.run() — seeds per-request context for all handlers)
8.  Rate limiting (multi-dimensional sliding window, Redis — skipped for trusted services)
9.  Error handler (catch-all → D2Result, uses TK.* constants from @d2/i18n)
    --- Public routes: /api/auth/check-email ---
10. Email availability check (public, pre-auth — rate limited but no session/CSRF)
    --- BetterAuth routes: /api/auth/* ---
11. Fingerprint AsyncLocalStorage (stores fp for JWT definePayload)
12. BetterAuth handler (with throttle on sign-in)
    --- Protected routes: /api/emulation/*, /api/org-contacts/*, /api/invitations/* ---
13. Session middleware (extracts user + session, fail-closed)
14. Session fingerprint validation (@d2/session-fingerprint — continuity check)
15. DI scope (creates per-request scope, merges auth into IRequestContext, upgrades ambient via enterWith)
16. CSRF protection (@d2/csrf — Origin header validation)
17. Authorization middleware (requireOrg, requireStaff, requireRole — visible at route declaration)
18. Thin route handlers (extract input → call handler → return result)
19. App-layer handlers (Zod validation → business logic → repository)
```

**API key requirement**: All auth endpoints require a valid `X-Api-Key` header (`require: true` on service-key middleware). Missing key → 401. Invalid key → 401. Valid key → `IRequestContext.isTrustedService = true`, continues to next middleware.

**Auth flag semantics** (`IRequestContext`): `isAuthenticated`, `isTrustedService`, `isOrgEmulating`, `isUserImpersonating` use `boolean | null` (three-state). `null` = "not yet determined" (pre-auth middleware), `false` = "confirmed not", `true` = "confirmed yes". Service-level contexts (composition roots) initialize emulation/impersonation to `null`, `isAuthenticated` to `false`. Never treat `null` as `false` in logic.

### .NET Gateway Middleware Order

Auth middleware has moved from gateway-local (`REST/Auth/`) to shared packages at `backends/dotnet/shared/Implementations/Middleware/JwtAuth.Default/`, `ServiceKey.Default/`, `AuthPolicy.Default/`, and `Translation.Default/`.

```
1.  Security headers (X-Content-Type-Options: nosniff, X-Frame-Options: DENY)
2.  CORS
3.  Request enrichment (IP resolution, fingerprint, WhoIs)
4.  Service key detection (ServiceKey.Default — X-Api-Key → sets IsTrustedService flag)
5.  Rate limiting (multi-dimensional sliding window — skipped for trusted services)
6.  Authentication (JwtAuth.Default — JWT Bearer via JWKS)
7.  Fingerprint validation (JwtAuth.Default — fp claim vs computed — skipped for trusted services)
8.  Request context logging
9.  Authorization (policy evaluation)
10. Idempotency (for POST/PUT/PATCH)
11. Translation (Translation.Default — resolves TK.* keys in D2Result responses)
12. Endpoint routing
```

---

## Fail-Closed vs Fail-Open Matrix

**Principle**: "Better to be offline than hacked." Auth checks must NEVER silently degrade to unauthenticated.

| Component                         | Redis Down                   | DB Down         | Auth Service Down                                            | Behavior                                                                    | Status Code              |
| --------------------------------- | ---------------------------- | --------------- | ------------------------------------------------------------ | --------------------------------------------------------------------------- | ------------------------ |
| **Session middleware** (Node.js)  | **FAIL-CLOSED**              | **FAIL-CLOSED** | N/A (is the auth service)                                    | Returns 503, does NOT treat as unauthenticated                              | 503                      |
| **JWT validation** (.NET)         | N/A (JWKS cached)            | N/A             | JWKS cache still valid → pass; cache empty → **FAIL-CLOSED** | Cached JWKS keys survive outage                                             | 401 if no cached keys    |
| **Session fingerprint** (Node.js) | FAIL-OPEN                    | N/A             | N/A                                                          | Fingerprint check skipped, logs warning                                     | 200 (pass-through)       |
| **JWT fingerprint** (.NET)        | N/A                          | N/A             | N/A                                                          | No fp claim (non-trusted) → **FAIL-CLOSED** (401). Trusted services → skip. | 401 (non-trusted, no fp) |
| **Service key detection** (.NET)  | N/A                          | N/A             | N/A                                                          | Invalid key → 401. No key → pass-through (browser request).                 | 401 (invalid key)        |
| **Service key detection** (Node)  | N/A                          | N/A             | N/A                                                          | `require: true` on auth: no key → 401. Invalid key → 401.                   | 401 (missing/invalid)    |
| **Sign-in throttle** (Node.js)    | FAIL-OPEN                    | N/A             | N/A                                                          | Throttle check skipped, sign-in proceeds normally                           | 200 (pass-through)       |
| **Rate limiting** (Node.js)       | FAIL-OPEN                    | N/A             | N/A                                                          | Requests pass through                                                       | 200 (pass-through)       |
| **Rate limiting** (.NET)          | FAIL-OPEN                    | N/A             | N/A                                                          | Requests pass through                                                       | 200 (pass-through)       |
| **Secondary storage** (Redis)     | FAIL-CLOSED (via session MW) | N/A             | N/A                                                          | BetterAuth getSession throws → 503                                          | 503                      |
| **CSRF**                          | N/A                          | N/A             | N/A                                                          | Always enforced (no external deps)                                          | 403                      |
| **CORS**                          | N/A                          | N/A             | N/A                                                          | Always enforced (no external deps)                                          | Blocked by browser       |
| **Authorization middleware**      | N/A                          | N/A             | N/A                                                          | Always enforced (reads from session)                                        | 401/403                  |

### Critical Fail-Closed Tests

The following tests verify fail-closed behavior:

| Test                                                   | File                             | What It Verifies                    |
| ------------------------------------------------------ | -------------------------------- | ----------------------------------- |
| `should return 503 when getSession throws`             | `auth/tests/.../session.test.ts` | Redis/DB outage → 503 (not 401)     |
| `should return 503 on infrastructure failure, not 401` | `auth/tests/.../session.test.ts` | Explicit: status is 503 AND NOT 401 |
| `should not call next when getSession throws`          | `auth/tests/.../session.test.ts` | Route handler never reached         |

---

## CQRS Handlers

### Custom Handlers (Application Layer)

These handlers operate on custom tables via Drizzle repositories. They follow the BaseHandler pattern with OTel tracing.

#### Commands (C/)

| Handler                | Input                                              | Side Effects                                                                                                                                                                                          | Zod Validation                                               | Business Logic                                                                                                                                                                                             |
| ---------------------- | -------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| RecordSignInOutcome    | identifierHash, identityHash, responseStatus       | Redis: mark known-good / increment failures / set lockout                                                                                                                                             | — (pre-hashed input)                                         | Success (200) → markKnownGood + clearFailureState + update local cache. Failure (401/400) → incrementFailures + computeSignInDelay → setLocked if delay > 0. Other status → no-op. Fail-open on all errors |
| RecordSignInEvent      | userId, ipAddress, userAgent, whoIsId?, successful | Writes to `sign_in_event`                                                                                                                                                                             | userId required                                              | —                                                                                                                                                                                                          |
| CreateEmulationConsent | userId, grantedToOrgId, activeOrgType, expiresAt   | Writes to `emulation_consent`                                                                                                                                                                         | zodGuid, z.enum(ORG_TYPES), future date, max 30d             | canEmulate check, org existence, duplicate prevention                                                                                                                                                      |
| RevokeEmulationConsent | consentId, userId                                  | Sets `revoked_at`                                                                                                                                                                                     | zodGuid × 2                                                  | Consent exists, belongs to user, is active                                                                                                                                                                 |
| CreateOrgContact       | organizationId, label, isPrimary?, contact         | Creates `org_contact` junction → Geo contact via gRPC (ext key = junction ID)                                                                                                                         | zodGuid, zodNonEmptyString, contact sub-fields               | Junction created first; Geo contact uses ext key `(org_contact, junction.id)`. Rollback junction on Geo failure                                                                                            |
| UpdateOrgContact       | id, organizationId, updates                        | Updates metadata or replaces contact via `updateContactsByExtKeys`                                                                                                                                    | zodGuid × 2, at-least-one-field refine, optional contact     | IDOR check; if contact provided: atomic replace via Geo ext-key update                                                                                                                                     |
| DeleteOrgContact       | id, organizationId                                 | Deletes junction + best-effort Geo contact cleanup via ext key                                                                                                                                        | zodGuid × 2                                                  | IDOR check; Geo delete failure tolerated (orphan cleanup by Geo job)                                                                                                                                       |
| CreateUserContact      | userId, email, name, locale                        | Creates Geo contact via gRPC (`contextKey: auth_user`)                                                                                                                                                | zodGuid, zodNonEmptyString(254/511/35)                       | Splits `name` into firstName/lastName; maps `locale` to `ietfBcp47Tag` on the Geo contact; Geo failure aborts sign-up entirely                                                                             |
| RequestUserDeletion    | userId, currentPassword, feedback?                 | Atomic password verify; CheckSoleOwnerOrgs; UpdateUserStatus → `pending_deletion`; DeleteAllUserSessions; bust BetterAuth Redis cookie cache; comms `alternativeContactInfo` "scheduled" notification | zodGuid, password required, optional `{ reason?, comment? }` | 401 on wrong password, 409 `SOLE_OWNER_OF_ORGS` if blocking, otherwise 200 `{ scheduledFor }` (ISO date = `deletedAt + GRACE_PERIOD_DAYS`). Notification bypasses channel preferences (security-relevant)  |
| CancelUserDeletion     | userId                                             | Idempotent UpdateUserStatus → `active`, clears `deletedAt`, sends "cancelled" notification                                                                                                            | zodGuid                                                      | No-op if status≠pending_deletion. Fired fire-and-forget by the `session.create.before` hook                                                                                                                |
| FinalizeDeletedUser    | userId                                             | AnonymizeUser; on success → "complete" notification via `alternativeContactInfo` (Geo contact is being torn down) + publish `auth.user-anonymize` fanout event                                        | zodGuid                                                      | Per-user worker. AnonymizeUser's `WHERE status='pending_deletion'` guard makes the run race-safe vs concurrent cancel                                                                                      |

#### Notification Publishing

Auth uses `@d2/comms-client` (configured in `@d2/auth-api` composition root) for all notification publishing. The comms client publishes to the `comms.notifications` fanout exchange via RabbitMQ with a universal notification shape (title + content markdown + plain text). Auth resolves contactId before publishing (e.g., via `GetContactsByExtKeys` for user contacts, or uses the contactId directly for invitation recipients).

Notifications are published from BetterAuth callbacks (`sendVerificationEmail`, `sendResetPassword`) and custom routes (invitation). No per-event publish handlers — the comms client is called directly.

#### Queries (Q/)

| Handler                | Input                           | Returns                       | Pagination / Caching                                                                                                                                           |
| ---------------------- | ------------------------------- | ----------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| CheckSignInThrottle    | identifierHash, identityHash    | `{ blocked, retryAfterSec? }` | N/A. Local memory cache for known-good (5min TTL, 0 Redis calls). Cache miss → concurrent `Promise.all(isKnownGood, getLockedTtlSeconds)`. Fail-open on errors |
| GetSignInEvents        | userId, limit?, offset?         | SignInEvent[] + total         | Default 50, max 100. Local memory cache with staleness check (latest event date)                                                                               |
| GetActiveConsents      | userId, limit?, offset?         | EmulationConsent[]            | Default 20, max 100                                                                                                                                            |
| GetOrgContacts         | organizationId, limit?, offset? | HydratedOrgContact[]          | Default 20, max 100. Batch-fetches Geo contact data via ext keys (`getContactsByExtKeys`), joins with junction metadata                                        |
| CheckEmailAvailability | email                           | `{ available: boolean }`      | In-memory cache: "taken" 1h TTL, "available" 30s TTL. Cache miss → repo query. Fail-open on cache errors                                                       |

### BetterAuth-Managed Operations

These are handled by BetterAuth directly (not custom handlers):

| Operation          | BetterAuth Method               | Notes                                                                                                                                                                                                                 |
| ------------------ | ------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Sign up            | `emailPassword.signUp`          | Pre-generates UUIDv7, creates Geo contact first (`CreateUserContact` hook with locale from `user.locale ?? BASE_LOCALE`). No auto-sign-in — user must verify email. Verification email via `@d2/comms-client` → Comms |
| Sign in (email)    | `emailPassword.signIn`          | Triggers `onSignIn` hook → RecordSignInEvent. Throttle-guarded                                                                                                                                                        |
| Sign in (username) | `username.signIn`               | Same hooks + throttle guard as email sign-in                                                                                                                                                                          |
| Sign out           | `signOut`                       | Revokes session from all 3 tiers                                                                                                                                                                                      |
| Get session        | `getSession`                    | 3-tier lookup                                                                                                                                                                                                         |
| Create org         | `organization.create`           | Validates orgType via `beforeCreateOrganization`                                                                                                                                                                      |
| Create invitation  | `auth.api.createInvitation`     | Called by custom `/api/invitations` route (NOT exposed via BetterAuth HTTP). `sendInvitationEmail` callback disabled — our route handles publishing                                                                   |
| Accept invitation  | `organization.acceptInvitation` | State machine transition                                                                                                                                                                                              |
| JWKS               | `/.well-known/jwks.json`        | Auto-rotated key pairs                                                                                                                                                                                                |
| Impersonation      | `admin.impersonateUser`         | 1-hour session, requires admin                                                                                                                                                                                        |

---

## Request Flow Diagrams

### How Authentication Works (Plain-Language Overview)

The system uses two complementary mechanisms: **sessions** (for browser-to-auth communication) and **JWTs** (for browser-to-gateway API calls). Understanding the difference is key.

**JWT** (JSON Web Token) is a signed, self-contained note. Auth Service creates it, signs it with a private key, and hands it to the client. The note says "this is user X, they belong to org Y, their role is Z, and this note expires in 15 minutes." Anyone who receives the note can read it — it's not encrypted — but nobody can forge it because they don't have the private key.

**JWKS** (JSON Web Key Set) is the corresponding public key, published by Auth Service at `/api/auth/jwks`. The .NET gateways download this key once, cache it, and use it to verify the signature on every incoming JWT — no per-request call to Auth Service needed.

**The three paths through the system:**

1. **Login (session creation)** — Browser talks to SvelteKit, which proxies to Auth Service. Auth validates credentials, creates a session (stored in Redis + PostgreSQL for durability), and sets a session cookie. This is the only cookie/session-based path.

2. **JWT issuance** — The browser asks Auth Service (via SvelteKit proxy) for a JWT by calling `authClient.token()`. Auth looks up the session, confirms it's valid, then mints a JWT containing the user's claims (userId, orgId, role, fingerprint) and signs it with the RS256 private key. The JWT is held in memory only — never localStorage (XSS risk).

3. **API calls** — The browser sends the JWT directly to the REST Gateway in the `Authorization` header. The gateway checks the signature against its cached JWKS public key (no network call to Auth). If valid and not expired, it trusts the claims inside and forwards the request via gRPC to internal services (Geo, Comms, etc.).

**Why this design?** Auth Service is the single source of truth for identity but is never a per-request bottleneck — gateways validate JWTs independently using cached public keys. Sessions give instant revocation (within 5 minutes when cookie cache expires). JWTs give stateless validation at the gateway. The 15-minute JWT expiry is the tradeoff: if a session is revoked, an existing JWT still works at the gateway for up to 15 minutes — short enough to be acceptable, long enough to avoid constant re-issuance.

---

### Browser → SvelteKit → Auth Service (Cookie Session)

```
Browser ──cookie──► SvelteKit Server
                    │
                    ├─ /api/auth/* ──proxy──► Auth Service (Hono)
                    │                         ├─ CSRF check
                    │                         ├─ Rate limit
                    │                         ├─ Session fingerprint
                    │                         └─ BetterAuth handler
                    │
                    └─ SSR pages
                       ├─ getSession (via proxy)
                       ├─ authClient.token() → JWT
                       └─ JWT ──► .NET Gateway ──gRPC──► Services
```

### Browser → .NET Gateway (JWT)

```
Browser ──JWT (in-memory)──► .NET Gateway
                              ├─ Request enrichment (IP, fingerprint, WhoIs)
                              ├─ Rate limiting (sliding window)
                              ├─ CORS
                              ├─ JWT validation (JWKS)
                              ├─ Fingerprint validation (fp claim)
                              ├─ Authorization (policy evaluation)
                              ├─ Idempotency (POST/PUT/PATCH)
                              └─ gRPC ──► Services
```

### JWT Flow

```
1. Client calls authClient.token() (via SvelteKit proxy)
2. SvelteKit proxies to Auth Service /api/auth/token
3. Auth Service validates session cookie
4. definePayload() builds JWT claims from user + session
5. Signs with RS256 private key (auto-rotated)
6. Returns JWT to client
7. Client stores in memory (NEVER localStorage — XSS risk)
8. Client sends JWT to .NET Gateway in Authorization header
9. Gateway validates via JWKS public key (cached)
```

---

## Email & Phone Change (OTP)

Account change flows for email and phone use a two-step OTP ritual: `request-change` (validates password, issues OTP) → `verify-change` (validates code, applies change). Both factors share storage, rate-limit, and attempt-budget mechanics, differing only in TTL and delivery channel.

### CQRS Handlers (`@d2/auth-app`)

| Handler              | Category | Purpose                                                                                            |
| -------------------- | -------- | -------------------------------------------------------------------------------------------------- |
| `RequestEmailChange` | Command  | Password-gated; issues 15-min email OTP, publishes comms notification to the PENDING new email     |
| `VerifyEmailChange`  | Command  | Validates OTP; applies email change; sends security notification to the OLD email                  |
| `RequestPhoneChange` | Command  | Password-gated; issues 5-min SMS OTP, publishes comms notification to the PENDING new phone number |
| `VerifyPhoneChange`  | Command  | Validates OTP; applies phone change + sets `phoneVerified: true`                                   |
| `RemovePhone`        | Command  | Password-gated; clears `phone` + `phoneVerified` (no OTP — password is the gate)                   |

### BetterAuth User Fields

Two new user-table fields added via `user.additionalFields`:

| Field           | Type                     | Notes                                                                                   |
| --------------- | ------------------------ | --------------------------------------------------------------------------------------- |
| `phone`         | `text`, nullable, unique | Unique partial index: `CREATE UNIQUE INDEX ... ON user (phone) WHERE phone IS NOT NULL` |
| `phoneVerified` | `boolean`, default false | Set to `true` only on successful `VerifyPhoneChange`                                    |

### OTP Storage (Verification Table Reuse)

OTPs piggyback on BetterAuth's existing `verification` table — no new schema. One active record per change type per user.

| Field        | Value                                                             |
| ------------ | ----------------------------------------------------------------- |
| `identifier` | `account-change:{type}:{userId}` where `type` ∈ {`email`,`phone`} |
| `value`      | JSON: `{ codeHash, pendingValue, attempts }`                      |
| `expiresAt`  | now + TTL (see below)                                             |

- `codeHash`: scrypt hash of the 6-digit code
- `pendingValue`: the new email / phone being claimed
- `attempts`: wrong-code counter

### OTP Constants (`@d2/auth-domain`)

| Constant                   | Value | Purpose                                      |
| -------------------------- | ----- | -------------------------------------------- |
| `OTP_EXPIRY.EMAIL_SECONDS` | 900   | 15 min — email OTP TTL                       |
| `OTP_EXPIRY.PHONE_SECONDS` | 300   | 5 min — SMS OTP TTL                          |
| `OTP_VERIFY.CODE_LENGTH`   | 6     | Numeric digits                               |
| `OTP_VERIFY.MAX_ATTEMPTS`  | 5     | Wrong-code tries before the record is burned |

On `attempts >= MAX_ATTEMPTS` the verification record is deleted — the user must restart with a fresh `request-change`.

### OTP Rate Limiter (`OtpRateLimitStore`, `@d2/auth-infra`)

Redis-backed send-rate limiter, keyed by `(userId, type)`. Mirrors `SignInThrottleStore` semantics:

| Sends in window | Outcome                                                           |
| --------------- | ----------------------------------------------------------------- |
| 1–3             | Free — no delay                                                   |
| 4+              | Exponential backoff: `min(2^(n-3) * 1min, 10 min)`, capped at 10m |

- Window: 5 min
- Fail-open on Redis errors (same as throttle store)
- Password failures do NOT consume the budget (verified before any rate-limit state change)

### Password Gate

Every initiate (`request-change`) AND `RemovePhone` takes `currentPassword` in the SAME request body as the new value. Verification uses `BetterAuthPasswordVerifier` and runs BEFORE any state change (OTP issuance, DB write, rate-limit consumption).

- Wrong password → 401 Unauthorized, no rate-limit consumed, no OTP issued
- Right password → proceeds to rate-limit check → OTP issuance

### HTTP Routes (`@d2/auth-api`)

All routes extract `userId` from the session (never from the request body — IDOR safe).

| Method   | Path                                | Handler              |
| -------- | ----------------------------------- | -------------------- |
| `POST`   | `/api/account/email/request-change` | `RequestEmailChange` |
| `POST`   | `/api/account/email/verify-change`  | `VerifyEmailChange`  |
| `POST`   | `/api/account/phone/request-change` | `RequestPhoneChange` |
| `POST`   | `/api/account/phone/verify-change`  | `VerifyPhoneChange`  |
| `DELETE` | `/api/account/phone`                | `RemovePhone`        |

### Notification Delivery

OTP notifications are published via `@d2/comms-client` using `alternativeContactInfo` (the pending new value is NOT yet a Geo contact, so no contactId exists):

| Flow                 | Target                                                                                         |
| -------------------- | ---------------------------------------------------------------------------------------------- |
| Email OTP            | `alternativeContactInfo: { email: pendingEmail }`                                              |
| Phone OTP            | `alternativeContactInfo: { phone: pendingPhone }`                                              |
| Email change success | `alternativeContactInfo: { email: OLD email }` — security notification to the previous address |

The old-email security notification ("your email was changed") fires after `VerifyEmailChange` applies the new email, giving the previous account owner a window to detect takeover.

---

## Cross-Service Consistency (SAGA Pattern)

Several handlers update both Geo (user/org contact data) and Auth (BetterAuth user/session rows) in a single logical operation. Without coordination, a failure between the two writes leaves the system inconsistent. The auth service uses a Geo-first, Auth-second, compensate-on-failure SAGA.

### Handlers Using the Pattern

- `UpdateUserLocale` — Geo contact `ietfBcp47Tag` + BetterAuth `user.locale`
- `UpdateUserTimezone` — Geo contact `ianaIdentifier` + BetterAuth `user.timezone`
- `UpdateUserRealName` — Geo contact `firstName`/`lastName` + BetterAuth `user.name`
- `UpdateOrgContact` — Geo contact replacement + `org_contact` junction metadata
- `VerifyEmailChange` — Geo contact email + BetterAuth `user.email`
- `VerifyPhoneChange` — Geo contact phone + BetterAuth `user.phone` / `phoneVerified`

### Shared Helper

`runCrossServiceUpdate` in `@d2/auth-app/cqrs/handlers/x/cross-service-update.ts` is the single implementation of the pattern. Handlers above delegate to it with three callbacks: `snapshot` (capture Geo pre-state), `applyGeo` (primary write), `applyAuth` (secondary write). It is a free function (not a `BaseHandler` subclass) — see [BACKENDS.md](../../BACKENDS.md) § "SAGA Pattern" for why this is a sanctioned exception to the BaseHandler shape.

### Flow

```
1. Snapshot Geo pre-state (read current value)
2. applyGeo(newValue)        ── if fails → bubbleFail (no Auth write, no state change)
3. applyAuth(newValue)       ── if fails → compensate (restore Geo to snapshot)
4. If compensation fails     ── logger.fatal(...) with full context → bubbleFail
5. Return Auth's original failure (never mask it)
```

### Rationale

Geo is the riskier call (gRPC, external service, network). Failing fast there avoids touching local Auth state entirely. If Auth then fails — a lower-probability event since it's local DB — we roll Geo back to the pre-update snapshot, preserving the "atomic from the user's POV" contract.

### Compensation-Rollback Failure (CRITICAL)

If the compensating Geo update ALSO fails, the helper logs at `logger.fatal()` with `traceId`, `userId`, the pre-snapshot, and the attempted new value. The system is now in a known-inconsistent state (Geo has `newValue`, Auth has `oldValue`) and **manual reconciliation is required**. This is the project-wide rule for any compensation-rollback failure: fatal severity, full context, never silent.

---

## Constants Cross-Reference

All auth constants are defined in two mirror locations and MUST stay in sync:

### JWT Claim Types

| Node.js (`JWT_CLAIM_TYPES`) | .NET (`JwtClaimTypes`)   | Value                     |
| --------------------------- | ------------------------ | ------------------------- |
| `SUB`                       | `SUB`                    | `"sub"`                   |
| `EMAIL`                     | `EMAIL`                  | `"email"`                 |
| `USERNAME`                  | `USERNAME`               | `"username"`              |
| `ORG_ID`                    | `ORG_ID`                 | `"orgId"`                 |
| `ORG_NAME`                  | `ORG_NAME`               | `"orgName"`               |
| `ORG_TYPE`                  | `ORG_TYPE`               | `"orgType"`               |
| `ROLE`                      | `ROLE`                   | `"role"`                  |
| `EMULATED_ORG_ID`           | `EMULATED_ORG_ID`        | `"emulatedOrgId"`         |
| `EMULATED_ORG_NAME`         | `EMULATED_ORG_NAME`      | `"emulatedOrgName"`       |
| `EMULATED_ORG_TYPE`         | `EMULATED_ORG_TYPE`      | `"emulatedOrgType"`       |
| `IS_EMULATING`              | `IS_EMULATING`           | `"isEmulating"`           |
| `IMPERSONATED_BY`           | `IMPERSONATED_BY`        | `"impersonatedBy"`        |
| `IS_IMPERSONATING`          | `IS_IMPERSONATING`       | `"isImpersonating"`       |
| `IMPERSONATING_EMAIL`       | `IMPERSONATING_EMAIL`    | `"impersonatingEmail"`    |
| `IMPERSONATING_USERNAME`    | `IMPERSONATING_USERNAME` | `"impersonatingUsername"` |
| `FINGERPRINT`               | `FINGERPRINT`            | `"fp"`                    |

### Auth Policies

| Node.js (`AUTH_POLICIES`) | .NET (`AuthPolicies`) | Value             |
| ------------------------- | --------------------- | ----------------- |
| `AUTHENTICATED`           | `AUTHENTICATED`       | `"Authenticated"` |
| `HAS_ACTIVE_ORG`          | `HAS_ACTIVE_ORG`      | `"HasActiveOrg"`  |
| `STAFF_ONLY`              | `STAFF_ONLY`          | `"StaffOnly"`     |
| `ADMIN_ONLY`              | `ADMIN_ONLY`          | `"AdminOnly"`     |

### Request Headers

| Node.js (`REQUEST_HEADERS`) | .NET (`RequestHeaders`) | Value                    |
| --------------------------- | ----------------------- | ------------------------ |
| `IDEMPOTENCY_KEY`           | `IDEMPOTENCY_KEY`       | `"Idempotency-Key"`      |
| `CLIENT_FINGERPRINT`        | `CLIENT_FINGERPRINT`    | `"X-Client-Fingerprint"` |

### OrgType Values

| Node.js (`ORG_TYPES`) | .NET (`OrgTypeValues`) | .NET Enum            |
| --------------------- | ---------------------- | -------------------- |
| `"admin"`             | `ADMIN`                | `OrgType.Admin`      |
| `"support"`           | `SUPPORT`              | `OrgType.Support`    |
| `"customer"`          | `CUSTOMER`             | `OrgType.Customer`   |
| `"third_party"`       | `THIRD_PARTY`          | `OrgType.ThirdParty` |
| `"affiliate"`         | `AFFILIATE`            | `OrgType.Affiliate`  |

**Note**: `"third_party"` maps to `OrgType.ThirdParty` in the handler layer.

### Role Values

| Node.js (`ROLES`) | .NET (`RoleValues`) | Hierarchy Level |
| ----------------- | ------------------- | --------------- |
| `"auditor"`       | `AUDITOR`           | 0 (lowest)      |
| `"agent"`         | `AGENT`             | 1               |
| `"officer"`       | `OFFICER`           | 2               |
| `"owner"`         | `OWNER`             | 3 (highest)     |

---

## Security Controls Summary

### Defense-in-Depth Layers

| Layer                | Control                                                         | Location                       |
| -------------------- | --------------------------------------------------------------- | ------------------------------ |
| Transport            | HTTPS (TLS)                                                     | Load balancer / reverse proxy  |
| CORS                 | Origin whitelist                                                | Both Node.js and .NET          |
| CSRF                 | Content-Type + Origin validation                                | Node.js (auth API)             |
| Rate Limiting        | Per-IP (Node.js), Multi-dimensional (.NET)                      | Both                           |
| Brute-Force Throttle | Progressive delay per (identifier, identity), known-good bypass | Node.js (auth API)             |
| Authentication       | Cookie session (SvelteKit) / JWT (gateway)                      | Both                           |
| Fingerprint Binding  | SHA-256(UA\|Accept) → session store + JWT claim                 | Both                           |
| Authorization        | Policy-based (orgType + role hierarchy)                         | Both                           |
| Input Validation     | Zod schemas in handlers (Node.js), FluentValidation (.NET)      | Both                           |
| IDOR Prevention      | Session-scoped org ID + handler ownership check                 | App-layer handlers             |
| Idempotency          | Redis-backed request dedup                                      | .NET gateway (external-facing) |

### Password Security

- Bcrypt hashing (BetterAuth default)
- Min 12 / max 128 characters (BetterAuth native enforcement)
- HIBP k-anonymity breach check (SHA-1 prefix, 24h cached, fail-open)
- Local common-password blocklist (~200 entries, always-on)
- Numeric-only and date-pattern blocking
- No composition rules (no "1 upper, 1 lower, 1 number, 1 special")

### Token Storage Rules

| Token            | Storage                              | Why                                                           |
| ---------------- | ------------------------------------ | ------------------------------------------------------------- |
| Session cookie   | HTTP-only, Secure, SameSite=Lax      | XSS-proof. CSRF protection via `lax` (blocks cross-site POST) |
| JWT              | In-memory only (JavaScript variable) | Never localStorage/sessionStorage (XSS risk)                  |
| JWKS private key | PostgreSQL (`jwks` table)            | Auto-rotated, never exposed                                   |

---

## Known Security Gaps & Pre-Production Requirements

These are documented trade-offs and gaps identified during security audit. Items are categorized by when they must be resolved.

### Must Fix Before Production

| #   | Gap                                              | Severity | Details                                                                                                                                                                                                                                                                                                                                                                                                                                                    | Mitigation                                                                       |
| --- | ------------------------------------------------ | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------- |
| 1   | ~~**Fingerprint (`fp`) claim optional in JWT**~~ | ~~HIGH~~ | **RESOLVED.** `JwtFingerprintMiddleware` now requires `fp` claim for all non-trusted requests (returns 401 `MISSING_FINGERPRINT`). Trusted services (identified by `ServiceKeyMiddleware` via `X-Api-Key`) skip fingerprint validation entirely.                                                                                                                                                                                                           | Implemented in `ServiceKeyMiddleware` + updated `JwtFingerprintMiddleware`.      |
| 2   | ~~**Email verification not enforced**~~          | ~~HIGH~~ | **RESOLVED.** `requireEmailVerification: true` blocks sign-in for unverified emails. `autoSignIn: false` prevents session creation at sign-up. `sendOnSignIn: true` re-sends on blocked sign-in attempt. `autoSignInAfterVerification: true` creates session after verification. Email delivery wired via `@d2/comms-client` → Comms service. OAuth providers set `emailVerified: true` automatically — verification only affects email/password sign-ups. | Implemented in `auth-factory.ts`. 3 integration tests in `auth-factory.test.ts`. |
| 3   | ~~**No password policy**~~                       | ~~HIGH~~ | **RESOLVED.** Min 12 / max 128 (BetterAuth native `minPasswordLength` / `maxPasswordLength`). HIBP k-anonymity check via SHA-1 prefix caching (24h TTL, fail-open). Local blocklist (~200 common passwords, always-on). Blocks numeric-only and date-pattern passwords. No composition rules.                                                                                                                                                              | Implemented in `password-rules.ts` (domain) + `password-hooks.ts` (infra).       |

### Must Fix Before Beta

| #   | Gap                                                    | Severity   | Details                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | Mitigation                                                                                       |
| --- | ------------------------------------------------------ | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| 4   | ~~**No per-account brute-force lockout**~~             | ~~MEDIUM~~ | **RESOLVED.** Progressive delay per (IP+fingerprint, email/username): 3 free attempts then escalating delays (5s → 15s → 30s → 1m → 5m → 15m max). Known-good identity bypass with local memory cache (0 Redis calls for legitimate users on known devices). Fail-open on Redis errors. Never hard-locks accounts. Both `/sign-in/email` and `/sign-in/username` protected.                                                                                                                                               | `CheckSignInThrottle` (query) + `RecordSignInOutcome` (command) + `SignInThrottleStore` (infra). |
| 5   | ~~**`sameSite` cookie attribute not explicitly set**~~ | ~~MEDIUM~~ | **RESOLVED.** `cookieOptions: { sameSite: "lax" }` set explicitly in BetterAuth config (`auth-factory.ts`). Protects against CSRF on POST while allowing link navigation.                                                                                                                                                                                                                                                                                                                                                 | Implemented in `auth-factory.ts`.                                                                |
| 6   | ~~**RecordSignInEvent missing Zod validation**~~       | ~~MEDIUM~~ | **RESOLVED.** Zod schema validates `userId` (UUID), `ipAddress` (max 45), `userAgent` (max 512), `whoIsId` (max 64 nullable). `validateInput()` called at top of `executeAsync()`.                                                                                                                                                                                                                                                                                                                                        | Implemented in `record-sign-in-event.ts`. 4 validation tests added.                              |
| 7   | ~~**Contact handler string fields unbounded**~~        | ~~MEDIUM~~ | **RESOLVED.** Zod schemas centralized in `@d2/geo-client` (`contactInputSchema`) as single source of truth — Auth handlers import and compose. Limits match Geo's EF Core/FluentValidation: names (255), title (20), generationalSuffix (10), company/job/department (255), addresses (255), city (255), postalCode (16), email (254), phone (20), labels (50), country code (2), subdivision (6), website (2048). .NET side: `ContactToCreateValidator` moved from `Geo.App` to `Geo.Client` for the same reuse pattern. | 5 boundary tests in `create-org-contact.test.ts`.                                                |
| 8   | ~~**`impersonatedBy` leaks admin identity in JWT**~~   | ~~MEDIUM~~ | **ACCEPTED RISK.** Admin user ID in JWT is an opaque UUIDv7 — reveals nothing about the admin account without database access. JWTs are short-lived (15min), transmitted over TLS, and fingerprint-bound. The value is needed for audit trails in downstream services. Hashing would lose the ability to look up the impersonator.                                                                                                                                                                                        | Accepted as low-risk design trade-off.                                                           |

### Track for GA

| #   | Gap                                                            | Severity | Details                                                                                                                                                                                                                                                                                                                                | Status                                                                                               |
| --- | -------------------------------------------------------------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| 9   | ~~**Rate limiting is single-instance (in-memory)**~~           | ~~LOW~~  | **RESOLVED.** Auth API uses `createDistributedRateLimitMiddleware` backed by `@d2/ratelimit` (Redis-backed sliding window via `@d2/interfaces`). Multi-instance safe.                                                                                                                                                                  | Implemented in `distributed-rate-limit.ts` middleware + composition root.                            |
| 10  | ~~**Session fingerprint fail-open on Redis error**~~           | ~~LOW~~  | **Not a gap.** Moved to Documented Assumptions — this is intentional (availability > security for defense-in-depth layer).                                                                                                                                                                                                             | See Documented Assumptions.                                                                          |
| 11  | ~~**CreateEmulationConsent DB error not caught gracefully**~~  | ~~LOW~~  | **RESOLVED.** PG unique violation (`23505`) handled in infra-layer repo handler (`CreateEmulationConsentRecord`) → returns D2Result.fail(409 Conflict). App handler (`CreateEmulationConsent`) uses `bubbleFail()` — no PG error codes in app layer. Non-PG errors propagate as thrown (BaseHandler wraps as 500 UNHANDLED_EXCEPTION). | Implemented in repo handler + app handler. 2 tests added (PG 23505 → 409 propagation, non-PG → 500). |
| 12  | ~~**GetActiveConsents no default pagination**~~                | ~~LOW~~  | **RESOLVED.** Default `limit: 50`, `offset: 0` applied via `input.limit ?? 50`, `input.offset ?? 0`. Existing `.max(100)` on limit unchanged.                                                                                                                                                                                          | Implemented in `get-active-consents.ts`. 2 tests added (default values, custom passthrough).         |
| 13  | ~~**JWKS caching not explicitly configured on .NET gateway**~~ | ~~LOW~~  | **RESOLVED.** `JwtAuthOptions.JwksAutoRefreshInterval` (8h) and `JwksRefreshInterval` (5min) now explicit and configurable. Auth JWKS endpoint returns `Cache-Control: public, max-age=3600`.                                                                                                                                          | Implemented in `JwtAuthOptions` + `JwtAuthExtensions` (.NET) and `auth-routes.ts` (Node.js).         |

### Documented Assumptions

These are intentional design decisions, not bugs:

| Assumption                                                     | Rationale                                                                                                                                                                                |
| -------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Session middleware fails closed (503) on infrastructure errors | "Better offline than hacked" — prevents auth degradation to unauthenticated                                                                                                              |
| JWT fingerprint uses `SHA-256(UA\|Accept)` only                | Lightweight check; not a substitute for TLS or token rotation. Attackers who control the UA+Accept headers AND have the JWT can bypass, but this raises the bar vs casual token theft    |
| Rate limiting fails open when Redis is down                    | Availability-first; DOS protection is best-effort                                                                                                                                        |
| Sign-in throttle fails open when Redis is down                 | Availability-first; brute-force protection is best-effort. Primary auth (session) still fails closed                                                                                     |
| Session fingerprint fails open when Redis is down              | Fingerprint binding is a defense-in-depth layer, not primary auth. Skipping the check on Redis outage preserves availability. Primary auth (session validation) still fails closed (503) |
| CSRF uses Content-Type + Origin dual check (no CSRF token)     | Hono auth routes are JSON-only APIs, not form submissions. Origin check prevents cross-site requests. Content-Type check prevents `<form>` submissions                                   |
| BetterAuth is session-based, JWTs are secondary                | JWTs are only for .NET gateway calls. Browser↔SvelteKit always uses cookie sessions                                                                                                      |

---

## Secure Endpoint Construction Checklist

When adding new routes/handlers, follow this checklist. Rules apply to **both Node.js (Auth Service) and .NET (Gateway)** unless noted.

### Route / Endpoint Layer

1. **Declare auth requirements visibly at route registration** — security must be readable at a glance:
   - Node.js (Hono): `app.post("/api/my-thing", requireOrg(), requireRole("officer"), async (c) => { ... });`
   - .NET (Minimal API): `group.MapPost("/my-thing", Handler).RequireAuthorization(AuthPolicies.HAS_ACTIVE_ORG);`
2. **Always require org context** for any org-scoped operation — Node.js: `requireOrg()`, .NET: `RequireAuthorization(AuthPolicies.HAS_ACTIVE_ORG)`
3. **Use staff/admin guards** for privileged operations — Node.js: `requireStaff()`, `requireAdmin()`. .NET: `RequireAuthorization(AuthPolicies.STAFF_ONLY)`, `RequireAuthorization(AuthPolicies.ADMIN_ONLY)`
4. **Use role-gated guards** leveraging the hierarchy (auditor < agent < officer < owner) — Node.js: `requireRole("officer")`. .NET: custom `RequireRole()` policy
5. **Keep route handlers thin** — extract input → call handler → return result. No business logic in routes/endpoints.

### Handler / Validation Layer

6. **Always validate input**:
   - Node.js: Zod via `this.validateInput(schema, input)` at the top of `executeAsync()`
   - .NET: FluentValidation or Data Annotations on request DTOs. Validate in handler or via pipeline behavior
   - All string fields: enforce max length (Node.js: `.max()`, .NET: `[MaxLength]` / `.MaximumLength()`) — never allow unbounded strings to reach the database
   - UUIDs: Node.js: `zodGuid` helper. .NET: use `Guid` type (implicit validation)
   - Enums: Node.js: `z.enum(VALID_VALUES)`. .NET: use typed enums, reject unknown values
   - Dates: Validate range (e.g., `expiresAt` must be in the future, max 30 days)
   - Nested objects: Validate all levels, not just top-level fields
7. **IDOR prevention** — always derive the org/user scope from the session (Node.js) or JWT claims (.NET), never from user-supplied input:
   ```
   CORRECT: org from session/claims (server-validated)
   WRONG:   org from request body (user-supplied, unverified)
   ```
8. **Ownership checks** — for update/delete, verify the entity belongs to the requesting user's org BEFORE performing the operation
9. **Handle DB constraint violations gracefully** — catch unique violation errors (PG code `23505`) and return 409 Conflict instead of 500
10. **Pagination defaults** — all list queries must have a DEFAULT limit (e.g., 50) and a MAX limit (e.g., 100). Never return unbounded result sets

### Infrastructure Layer

11. **Parameterized queries only** — Node.js: Drizzle handles this. .NET: EF Core handles this. Never concatenate user input into raw SQL
12. **Migrations are append-only** — Node.js: never modify existing Drizzle migration files, use `drizzle-kit generate`. .NET: never modify existing EF Core migrations, use `dotnet ef migrations add`

### Cross-Platform (JWT Claims, Policies, Constants)

13. **New JWT claims** — add to both `JWT_CLAIM_TYPES` (Node.js) and `JwtClaimTypes` (.NET). Keep cross-reference in AUTH.md § Constants Cross-Reference
14. **New policies** — add to both `AUTH_POLICIES` (Node.js) and `AuthPolicies` (.NET). Register via `AddD2Policies()`
15. **Sensitive data in JWT** — do NOT embed data that reveals internal structure (admin user IDs, internal audit data). Keep sensitive data in server-side session only

---

## Test Coverage Map

### .NET Tests (Gateway Auth)

| Test File                                  | Tests  | Coverage                                                                                                            |
| ------------------------------------------ | ------ | ------------------------------------------------------------------------------------------------------------------- |
| `JwtFingerprintValidatorTests.cs`          | 8      | SHA-256 formula, cross-platform parity, edge cases                                                                  |
| `JwtFingerprintMiddlewareTests.cs`         | 8      | Match/mismatch, no fp claim, no auth, case-insensitive, D2Result body, short claim                                  |
| `RequestContextJwtTests.cs`                | 14     | All claim extraction, OrgType mapping, missing claims, target header, impersonation                                 |
| `RequestContextDeriveRelationshipTests.cs` | 15     | All 5 DeriveRelationship branches, edge cases, null HttpContext                                                     |
| `AuthPolicyTests.cs`                       | 14     | AtOrAbove (5), OrgTypeValues (2), AddD2Policies (4), RequireOrgType (1), RequireRole (1), RequireOrgTypeAndRole (1) |
| **Subtotal**                               | **59** |                                                                                                                     |

### Node.js Tests (Auth Service)

| Test File                                       | Tests    | Coverage                                                                                                                                                                             |
| ----------------------------------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **API Layer**                                   |          |                                                                                                                                                                                      |
| `middleware/authorization.test.ts`              | 42       | requireOrg, requireOrgType, requireRole, requireStaff, requireAdmin, composition, defensive edge cases (empty/case/numeric/null/whitespace)                                          |
| `middleware/session.test.ts`                    | 10       | Success, 401 unauthenticated, 503 fail-closed, header forwarding, undefined return, non-Error throws, error detail leakage                                                           |
| `middleware/session-fingerprint.test.ts`        | 16       | SHA-256 computation, mismatch revocation, Redis storage, fail-open, Bearer, empty tokens, session isolation, best-effort revocation                                                  |
| `middleware/csrf.test.ts`                       | 21       | Content-Type + Origin validation, form attacks (text/plain, multipart, x-www-form-urlencoded), null origin, port/scheme mismatch                                                     |
| `middleware/scope.test.ts`                      | 18       | IRequestContext mapping (all 5 org types), emulation, traceId, disposal, unknown/PascalCase org types, empty emulatedOrgId, no-org session                                           |
| `middleware/error-handler.test.ts`              | 8        | D2Result-shaped error responses                                                                                                                                                      |
| `middleware/jwt-fingerprint.test.ts`            | 9        | JWT fp claim computation                                                                                                                                                             |
| `routes/auth-routes.test.ts`                    | 27       | Cache-Control on .well-known, throttle guard (429/Retry-After/blocked/allowed), fire-and-forget record, edge cases, case-insensitive hashing, username path, requestInfo propagation |
| `routes/check-email-routes.test.ts`             | 8        | Public email availability endpoint, missing param, success/error responses                                                                                                           |
| `routes/emulation-routes.test.ts`               | 26       | Middleware auth (role + staff), input mapping, status codes, pagination                                                                                                              |
| `routes/org-contact-routes.test.ts`             | 25       | Middleware auth (role), input mapping, status codes, pagination, no-org, contact details                                                                                             |
| `routes/invitation-routes.test.ts`              | 33       | Role checks, input validation, happy paths, defensive security (CRLF injection, IDOR, long inputs, concurrent requests)                                                              |
| **Domain Layer**                                |          |                                                                                                                                                                                      |
| `domain/enums/*.test.ts`                        | 15       | OrgType, Role, InvitationStatus validation + guards                                                                                                                                  |
| `domain/entities/*.test.ts`                     | 80       | Factory functions, immutability, validation                                                                                                                                          |
| `domain/rules/*.test.ts`                        | 87       | Emulation (self-emulation, forced auditor), membership, org-creation, invitation state machine, sign-in throttle, username, password                                                 |
| `domain/exceptions/*.test.ts`                   | 12       | Error structures                                                                                                                                                                     |
| **Infra Layer**                                 |          |                                                                                                                                                                                      |
| `infra/access-control.test.ts`                  | 5        | RBAC permission definitions                                                                                                                                                          |
| `infra/mappers/*.test.ts`                       | 15       | BetterAuth → domain mapping (5 mappers)                                                                                                                                              |
| `infra/secondary-storage.test.ts`               | 6        | Redis adapter contract                                                                                                                                                               |
| `infra/username-hooks.test.ts`                  | 7        | Username generation hooks                                                                                                                                                            |
| `infra/sign-in-throttle-store.test.ts`          | 11       | Redis key prefixes, handler delegation, TTL conversion, failure fallbacks                                                                                                            |
| **App Layer**                                   |          |                                                                                                                                                                                      |
| `app/handlers/c/*.test.ts`                      | 80       | All command handlers (create-emulation-consent, create-org-contact, delete-org-contact, update-org-contact, record-sign-in-event, record-sign-in-outcome)                            |
| `app/handlers/q/*.test.ts`                      | 70       | All query handlers (check-sign-in-throttle, get-active-consents, get-org-contacts, get-sign-in-events, check-email-availability)                                                     |
| **Integration Tests**                           |          |                                                                                                                                                                                      |
| `integration/migration.test.ts`                 | 16       | All 11 tables exist, columns correct, indexes verified, idempotent, partial unique index                                                                                             |
| `integration/custom-table-repositories.test.ts` | 26       | All 3 repos (CRUD, pagination, ordering, cross-contamination, unique constraints)                                                                                                    |
| `integration/auth-factory.test.ts`              | 21       | Email verification, full E2E flow, UUIDv7, session hooks, user row completeness, JWT claims, org+orgType, password validation, session fields, createUserContact hook                |
| `integration/better-auth-tables.test.ts`        | 8        | Sign-up, email uniqueness, sessions, orgs+members, JWKS/JWT issuance                                                                                                                 |
| `integration/secondary-storage.test.ts`         | 6        | Redis adapter integration (set/get/delete, TTL expiration)                                                                                                                           |
| `integration/session-fingerprint.test.ts`       | 7        | Redis-backed fingerprint storage + revocation                                                                                                                                        |
| `integration/sign-in-throttle-store.test.ts`    | 11       | Redis sign-in throttle store integration                                                                                                                                             |
| `integration/app-handlers.test.ts`              | 9        | App handler integration with DB (repositories + domain)                                                                                                                              |
| `integration/better-auth-behavior.test.ts`      | 31       | RS256 JWT structure, session lifecycle, additionalFields auto-enrichment (`session.update.before` hook), definePayload org context, snake_case columns, pre-generated UUIDv7 IDs     |
| **Total auth-tests**                            | **~865** | ~720 unit + ~135 integration (Testcontainers PostgreSQL 18 + Redis)                                                                                                                  |

### Key Security Behaviors Verified by Tests

| Security Property                                                                           | Verified By                                                                                 |
| ------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Sign-up does NOT auto-sign-in (email unverified)                                            | `auth-factory.test.ts`                                                                      |
| Sign-in blocked when email not verified                                                     | `auth-factory.test.ts`                                                                      |
| Sign-in succeeds after email verification                                                   | `auth-factory.test.ts`                                                                      |
| Verification email delivery via `@d2/comms-client` → Comms                                  | `auth-factory.test.ts`                                                                      |
| Session middleware fails closed (503, not 401) on infra errors                              | `session.test.ts`                                                                           |
| JWT fingerprint mismatch returns 401                                                        | `JwtFingerprintMiddlewareTests.cs`                                                          |
| Short fingerprint claims don't crash middleware                                             | `JwtFingerprintMiddlewareTests.cs`                                                          |
| No fp claim (non-trusted) → 401 MISSING_FINGERPRINT                                         | `JwtFingerprintMiddlewareTests.cs`                                                          |
| Only RS256 algorithm accepted                                                               | `JwtAuthExtensions.cs` configuration, `better-auth-behavior.test.ts`                        |
| Signed tokens required                                                                      | `JwtAuthExtensions.cs` configuration                                                        |
| Authorization policies require authenticated user                                           | `AuthPolicyTests.cs`                                                                        |
| Role hierarchy correctly computed (AtOrAbove)                                               | `AuthPolicyTests.cs`, `role.test.ts`                                                        |
| Emulation forced to auditor role                                                            | `emulation.test.ts`                                                                         |
| IDOR prevention (org ID from session, ownership check in handler)                           | `update-org-contact.test.ts`, `delete-org-contact.test.ts`                                  |
| Input validation (Zod schemas: UUID, string length, date range)                             | Handler test files (`create-*.test.ts`, `update-*.test.ts`, `record-sign-in-event.test.ts`) |
| PG unique violation (23505) → 409 Conflict (not 500)                                        | `create-emulation-consent.test.ts`                                                          |
| Default pagination (limit 50, offset 0) on list queries                                     | `get-active-consents.test.ts`                                                               |
| Contact string fields bounded (aligned with Geo: names ≤255, phone ≤20, company ≤255, etc.) | `create-org-contact.test.ts`                                                                |
| Brute-force throttle blocks with 429 + Retry-After header                                   | `auth-routes.test.ts`                                                                       |
| Brute-force throttle never blocks known-good identities (even during attack)                | `check-sign-in-throttle.test.ts`                                                            |
| Brute-force throttle fail-open on Redis/cache errors                                        | `check-sign-in-throttle.test.ts`, `record-sign-in-outcome.test.ts`                          |
| Successful sign-in marks identity as known-good + clears failures                           | `record-sign-in-outcome.test.ts`                                                            |
| Progressive delay escalates correctly (3 free → 5s → ... → 15m max)                         | `sign-in-throttle-rules.test.ts`                                                            |
| Null/missing HttpContext returns safe defaults                                              | `RequestContextDeriveRelationshipTests.cs`                                                  |
| Unknown orgType returns null (not exception)                                                | `RequestContextDeriveRelationshipTests.cs`                                                  |
| All 5 DeriveRelationship branches tested                                                    | `RequestContextDeriveRelationshipTests.cs`                                                  |

---

## File Index

### Node.js Auth Service

```
backends/node/services/auth/
├── domain/                              # @d2/auth-domain
│   └── src/
│       ├── constants/auth-constants.ts  # JWT_CLAIM_TYPES, SESSION_FIELDS, AUTH_POLICIES, REQUEST_HEADERS, PASSWORD_POLICY, GEO_CONTEXT_KEYS, SIGN_IN_THROTTLE
│       ├── entities/                    # User, Organization, Member, Invitation, SignInEvent, EmulationConsent, OrgContact
│       ├── enums/                       # OrgType, Role, InvitationStatus (+ type guards)
│       ├── exceptions/                  # AuthDomainError, AuthValidationError
│       ├── rules/                       # emulation, membership, org-creation, invitation, sign-in-throttle (delay curve)
│       ├── value-objects/               # SessionContext
│       └── index.ts                     # Public API
├── app/                                 # @d2/auth-app (TLC: mirrors Geo.App)
│   └── src/
│       ├── implementations/
│       │   └── cqrs/
│       │       └── handlers/
│       │           ├── c/               # Command handlers (8) — RecordSignInOutcome, CreateUserContact + 6 with Zod validation
│       │           └── q/               # Query handlers (4) — CheckSignInThrottle + 3 with Zod validation
│       ├── interfaces/
│       │   └── repository/
│       │       ├── handlers/            # Repo handler interfaces (23) + bundle types (TLC: c/, r/, u/, d/)
│       │       └── sign-in-throttle-store.ts  # ISignInThrottleStore (already delegates to cache handlers)
│       ├── service-keys.ts              # ServiceKey<T> constants for all handler interfaces
│       ├── registration.ts              # addAuthApp(services, options) — DI registration
│       └── index.ts                     # Re-exports + service keys
├── infra/                               # @d2/auth-infra (TLC: mirrors Geo.Infra)
│   └── src/
│       ├── auth/
│       │   ├── better-auth/
│       │   │   ├── auth-factory.ts      # BetterAuth creation (single source of truth)
│       │   │   ├── auth-config.ts       # Config type + defaults
│       │   │   ├── secondary-storage.ts # Redis adapter for BetterAuth
│       │   │   ├── access-control.ts    # RBAC permission definitions
│       │   │   └── hooks/               # id-hooks (UUIDv7), org-hooks (orgType validation)
│       │   └── sign-in-throttle-store.ts # Redis-backed brute-force throttle (Exists, GetTtl, Set, Remove, Increment)
│       ├── repository/
│       │   ├── schema/                  # Drizzle pgTable declarations (better-auth-tables.ts, custom-tables.ts, types.ts)
│       │   ├── handlers/               # BaseHandler repo handlers (TLC layout)
│       │   │   ├── c/                  # Create handlers (3): SignInEvent, EmulationConsent, OrgContact
│       │   │   ├── r/                  # Read handlers (8): find/count/getLatest for all 3 aggregates
│       │   │   ├── u/                  # Update handlers (2): RevokeEmulationConsent, UpdateOrgContact
│       │   │   ├── d/                  # Delete handlers (1): DeleteOrgContact
│       │   │   ├── factories.ts        # createXxxRepoHandlers(db, ctx) — 3 factory functions
│       │   │   └── utils/pg-errors.ts  # isPgUniqueViolation helper
│       │   ├── migrations/              # Auto-generated SQL (drizzle-kit generate)
│       │   └── migrate.ts              # Programmatic migration runner
│       ├── mappers/                     # BetterAuth → domain (user, org, member, invitation, session)
│       ├── registration.ts              # addAuthInfra(services, db) — DI registration
│       └── index.ts
├── api/                                 # @d2/auth-api
│   └── src/
│       ├── composition-root.ts          # DI assembly via ServiceCollection + ServiceProvider
│       ├── middleware/
│       │   ├── authorization.ts         # requireOrg, requireOrgType, requireRole, requireStaff, requireAdmin
│       │   ├── session.ts               # Session extraction (fail-closed)
│       │   ├── session-fingerprint.ts   # Thin Hono adapter around @d2/session-fingerprint
│       │   ├── scope.ts                 # Per-request DI scope (createScopeMiddleware)
│       │   ├── ambient-scope.ts         # AsyncLocalStorage-based ambient context (createAmbientScopeMiddleware)
│       │   ├── csrf.ts                  # Thin Hono adapter around @d2/csrf (Content-Type + Origin)
│       │   ├── cors.ts                  # CORS middleware factory
│       │   ├── service-key.ts           # Thin Hono adapter around @d2/service-key
│       │   ├── request-enrichment.ts    # @d2/request-enrichment middleware
│       │   ├── request-context-logging.ts # Per-request child logger with network bindings
│       │   ├── distributed-rate-limit.ts # @d2/ratelimit middleware
│       │   └── error-handler.ts         # D2Result error responses (uses TK.* from @d2/i18n)
│       ├── routes/
│       │   ├── auth-routes.ts           # BetterAuth routes + sign-in throttle guard
│       │   ├── check-email-routes.ts    # Public email availability check (pre-auth)
│       │   ├── emulation-routes.ts      # Thin routes: visible auth middleware, handler delegation
│       │   ├── org-contact-routes.ts    # Thin routes: visible auth middleware, handler delegation
│       │   ├── invitation-routes.ts     # Custom invitation route (requireOrg + requireRole(officer))
│       │   └── health.ts               # Health check
│       ├── services/
│       │   ├── auth-grpc-service.ts     # AuthServiceServer gRPC implementation (health checks)
│       │   ├── auth-jobs-grpc-service.ts # Auth job gRPC service
│       │   └── file-callback-grpc-service.ts # FileCallbackService (OnFileProcessed + CanAccess) — called by Files service
│       ├── setup/
│       │   └── hono-app-setup.ts        # Hono app builder with full middleware pipeline
│       └── index.ts
├── tests/                               # @d2/auth-tests (~865 tests)
│   └── src/
│       ├── unit/
│       │   ├── api/
│       │   │   ├── middleware/          # Session, fingerprint, authorization, CSRF, request-enrichment, rate-limit, error tests
│       │   │   └── routes/             # Route tests (auth throttle, emulation, org-contact)
│       │   ├── app/handlers/
│       │   │   ├── c/                  # Command handler tests (create/revoke/record/throttle + geo integration)
│       │   │   └── q/                  # Query handler tests (throttle, caching, hydration)
│       │   ├── domain/                 # Entity, enum, rule, throttle delay curve tests
│       │   └── infra/                  # Mapper, storage, access control, throttle store tests
│       └── integration/
│           ├── postgres-test-helpers.ts # Testcontainers PostgreSQL lifecycle
│           ├── migration.test.ts       # All 11 tables + indexes verified
│           ├── custom-table-repositories.test.ts  # 3 repos vs real PG
│           └── better-auth-tables.test.ts         # BetterAuth CRUD via Drizzle adapter
```

### .NET Gateway Auth

Auth middleware has moved from gateway-local (`REST/Auth/`) to three sibling shared middleware packages: `JwtAuth.Default` (JWT Bearer + fingerprint), `ServiceKey.Default` (X-Api-Key trust), and `AuthPolicy.Default` (named policies + endpoint extensions). The corresponding namespaces are `D2.Shared.JwtAuth.Default`, `D2.Shared.ServiceKey.Default`, and `D2.Shared.AuthPolicy.Default`.

```
backends/dotnet/shared/Implementations/Middleware/JwtAuth.Default/
├── JwtAuthExtensions.cs                 # AddJwtAuth + UseJwtAuth (JWKS, policies)
├── JwtAuthOptions.cs                    # Config: BaseUrl, Issuer, Audience, ClockSkew
├── JwtFingerprintMiddleware.cs          # fp claim validation (fail-closed for non-trusted, skip for trusted)
└── JwtFingerprintValidator.cs           # SHA-256(UA|Accept) computation

backends/dotnet/shared/Implementations/Middleware/ServiceKey.Default/
├── ServiceKeyExtensions.cs              # AddServiceKeyAuth + UseServiceKeyDetection
├── ServiceKeyMiddleware.cs              # X-Api-Key middleware (constant-time comparison) — sets IsTrustedService flag
├── ServiceKeyEndpointFilter.cs          # RequireServiceKey() endpoint filter
└── ServiceKeyOptions.cs                 # Service key config + key name mapping

backends/dotnet/shared/Implementations/Middleware/AuthPolicy.Default/
├── AuthPolicyExtensions.cs              # AddD2Policies, RequireOrgType, RequireRole, RequireOrgTypeAndRole
└── RoutePolicyExtensions.cs             # RequireAuth(), RequireOrg(), RequireStaff(), RequireAdmin() endpoint extensions

backends/dotnet/shared/Implementations/Middleware/Translation.Default/
├── TranslationMiddleware.cs             # TK.* key resolution in D2Result responses
├── LocaleResolver.cs                    # Locale resolution from D2-Locale header / Accept-Language
└── Extensions.cs                        # AddTranslation + UseTranslation

backends/dotnet/shared/Handler/Auth/
├── JwtClaimTypes.cs                     # JWT claim type constants
├── OrgTypeValues.cs                     # Org type constants + STAFF/ALL arrays
├── RoleValues.cs                        # Role constants + HIERARCHY + AtOrAbove()
├── AuthPolicies.cs                      # Named policy constants
└── RequestHeaders.cs                    # Custom header constants
```
