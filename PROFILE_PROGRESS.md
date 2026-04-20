# Profile Feature Progress

## Phase 1: Backend — Auth Handlers + API Routes

- [x] 1A. CQRS Handlers (UpdateUserRealName, UpdateUsername)
- [x] 1B. Repository Interfaces
- [x] 1C. Repository Implementations (Drizzle)
- [x] 1D. HTTP Routes (Hono account-routes)
- [x] 1E. Password Change Notification Hook
- [x] 1F. i18n Keys
- [x] 1G. Unit Tests (12 new tests for UpdateUserRealName, 18 for UpdateUsername — 1026 total pass)
- [x] **Review Checkpoint 1** — auth-tests pass (1029), tsc clean, lint clean, format clean

## Phase 2: Reusable SvelteKit Components

- [x] 2A. ConfirmationDialog
- [x] 2B. InlineEditField (always-editable, dirty detection, sync+async validation, save/undo icons)
- [x] 2C. InlineEditFieldGroup
- [x] 2C-1. UnsavedChangesBar (fixed bottom bar, sequential save all, discard all)
- [x] 2D. UserAvatarMenu (mode/theme segmented controls, language modal, profile link, sign out)
- [x] 2D-1. SegmentedControl (animated pill via Svelte Tween)
- [x] 2D-2. LanguageModal (locale selection with flag icons)
- [x] 2E. SettingsNav (vertical desktop / horizontal mobile)
- [x] 2F. InlineSwitch (on/off toggle for notification prefs)
- [x] 2G. InlineDropdown (single-value select for locale, timezone, etc.)
- [x] 2H. Debug demo pages (4-tab account-components: profile, email-phone, security, deactivate)
- [x] 2I. Mobile Public Nav (responsive, preferences dropdown, consolidated across layouts)
- [x] 2J. Component Tests (updated existing tests for nav/theme changes, 565 tests pass)
- [x] **Review Checkpoint 2** — svelte-check clean, lint clean, format clean, 565 tests pass

## Phase 3: Account Route Group + Profile Tab

- [x] 3A. Route Group + Layout (`/account/` with PublicNav + SettingsNav, `requireAuth()` guard)
- [x] 3B. Profile Tab (real save handlers for name + username, mock save for locale/timezone)
- [x] 3C. Delete old profile placeholder
- [x] 3D. Account API Proxy (`/api/account/[...path]` → Auth service)
- [x] 3E. Stub pages (email-phone, security with danger zone consolidated)
- [x] 3F. i18n keys (50+ `account_*` keys, translated in all 10 locales)
- [x] 3G. Fix theme/mode infinite loop (SegmentedControl `onchange`, removed local state + effects)
- [x] 3H. Fix user-avatar-menu `goto` → `resolve()` for `/account/profile`
- [x] **Review Checkpoint 3** — committed, container restarted

## Phase 4: SignalR + Avatar Upload + Real-Time Session Refresh

- [x] 4A. SignalR Client Infrastructure (`@microsoft/signalr`, JWT via query param, Svelte 5 runes, SvelteMap/SvelteSet)
- [x] 4B. SignalR Gateway fixes (CORS, `MapInboundClaims=false`, custom `JwksConfigurationRetriever`)
- [x] 4C. Avatar Upload Client (browser → Files REST API presigned PUT URL → MinIO)
- [x] 4D. Client-side crop/zoom editor (pure Canvas, circle mask, exports 512×512 WebP)
- [x] 4E. AvatarUploader Component (state machine: idle→cropping→uploading→processing→ready)
- [x] 4F. Wire avatar to Profile page + nav (presigned GET URLs, in-memory cache)
- [x] 4G. Files service security audit (access control on download route, IDOR fixes, app-layer refactor)
- [x] 4H. PushUserUpdated (Auth → SignalR Gateway gRPC, wired into 3 mutation handlers)
- [x] 4I. Browser `user:updated` listener (cache bust → `invalidateAll()` → reactive UI update)
- [x] 4J. InvalidateUserSessionCache command (bust BetterAuth Redis session cache after user mutations)
- [x] 4K. DI abstraction fix (moved distributed cache service keys from `@d2/cache-redis` to `@d2/interfaces`, added `addRedisCaching()`, fixed auth/files/comms app layers)
- [x] 4L. Integration tests (8 tests for session cache invalidation, 1037 auth-tests total)
- [x] **Review Checkpoint 4** — real-time updates working E2E, all tests pass, lint/format clean

## Phase 4B: Language & Timezone Persistence

- [x] 4M. Avatar removal (DELETE /api/account/avatar, frontend Remove button)
- [x] 4N. Account client module (account-client.ts — updateName, updateUsername, removeAvatar, updateLocale, bustSessionCache)
- [x] 4O. Skeleton loading states (profile page fields, avatar, header avatar; email-phone tab fields/toggles (commit 360d0e3e))
- [x] 4P. Contact data loss fix (fetch existing contact before UpdateContactsByExtKeys, abort on Geo failure)
- [x] 4Q. Locale persistence (UpdateUserLocale CQRS handler + repo, BFF types, sign-in hydration cookie sync)
- [x] 4R. Shared changeLocale() utility (both language modal + profile dropdown, awaits API + cache bust before reload)
- [x] 4S. Language change confirmation dialog (warns about page reload + unsaved changes)
- [x] 4T. Professional toast copy (no exclamation marks, "Changes saved." instead of "Save")
- [x] 4U. Error handler structured logging (createErrorHandler factory with Pino logger)
- [x] 4V. Timezone Geo domain entity + seed data (309 canonical IANA entries, EF Core config, proto, GetReferenceData)
- [x] 4W. Timezone frontend option transforms (priority pinning, label formatting)
- [x] 4X. EF migration for timezones + ref data version bump
- [x] 4Y. Auth user.timezone persistence (mirrors locale — handler, route, BFF, sign-in)
- [x] 4Z. Profile page timezone typeahead (searchable, real data from Geo ref data)
- [x] 4AA. Fix Paraglide setLocale() intermittent silent reload skip (own reload, bypass guard)
- [x] 4AB. Add IANAIdentifier (timezone) to Contact entity (domain, EF, mapper, validator, proto, migration, tests)
- [x] 4AC. UpdateUserTimezone handler syncs timezone to Geo contact (fetch-then-merge pattern)
- [x] 4AD. Dropdown shows current locale endonym + timezone display name instead of generic labels
- [x] **Review Checkpoint 4B** — language + timezone fully working E2E

## Phase 5: Security, Sessions, Recent Logins (full implementation)

- [x] 5A. Change Password modal (current + new + confirm, BetterAuth changePassword, optional revoke-others, security email via existing publishPasswordChanged hook)
- [x] 5B. Active Sessions list (GET /api/account/sessions enriched with WhoIs + isCurrent flag, UA parsing via ua-parser-js@2.0.9, password-gated per-session revoke + revoke-others)
- [x] 5C. Recent Logins (paginated sign-in events, GetSignInEvents enriched with WhoIs server-side; **Leaflet map with WhoIs locations deferred** — see below)
- [x] 5D. Email & Phone tab — email display + verified badges, phone add/change/remove with OTP, notification preferences (gateway → Comms via user-centric RPC), skeleton loading states, immediate-save toggles
- [x] 5E. Notification preferences — gateway + Comms wiring, i18n + defaults fix
- [x] 5F. Session WhoIs hydration — `session.who_is_id` column + Drizzle migration `0009`, `IFindActiveSessionsByUserId` repo handler, async resolution via existing WhoIs RabbitMQ consumer (extended with optional `sessionId` field)
- [x] 5G. Saga consistency for revoke routes — `currentPassword` is sent in the same HTTP body as the action (atomic, mirrors email/phone OTP pattern), `BetterAuthPasswordVerifier` checked BEFORE any state change, 401 on mismatch
- [ ] **Review Checkpoint 5** — security/sessions/logins fully functional

**Explicitly deferred (not done in this pass):**

- **5C-map**: Leaflet map view of recent-login WhoIs locations (Phase 6 polish — needs Leaflet install, marker clustering for repeat IPs, per-row map-toggle UI). All the data is already on the response (lat/lng would require expanding `LocationDTO` with coordinates — currently only city/subdivision/country are returned).
- **Delete Account flow**: still a stub-with-toast. Needs its own design conversation: cascade across services (Auth user + memberships, Geo contact, Comms threads, Files objects), grace period vs. hard delete, audit-trail retention. Not in scope for the security tab.
- **Country-name resolution in WhoIs display**: `formatLocation()` currently shows raw ISO codes (e.g. "Toronto, ON, CA"). The user has Geo ref data available server-side; the layout loader does not currently propagate the `countries` map to page data. Plumbing it through is the next polish step — the UI is forward-compatible, just swap raw codes for `displayName` lookups.
- **CountryDTO/SubdivisionDTO `displayName` lookup endpoints in `+layout.server.ts`**: tracked alongside the country-name resolution above.

## Phase 6: Navigation Updates

- [ ] 6A. App Header (UserAvatarMenu)
- [ ] 6B. App Sidebar (interactive footer with avatar)
- [ ] 6C. Remove old Profile nav link
- [ ] **Review Checkpoint 6** — nav works across all contexts

## Phase 7: Testing + Documentation

- [ ] 7A. Frontend Tests
- [ ] 7B. E2E Tests
- [ ] 7C. Documentation
- [ ] **Review Checkpoint 7 (Final)** — all tests pass, docs updated

## Known Issues

- [x] InlineDropdown info icon overlaps with select content (Language & Timezone dropdowns)
- [x] InlineEditFieldGroup shows redundant success checkmarks (one per field + one for the group)
- [ ] Pass locale + timezone through on sign-up from the frontend
- [ ] Dkron job to periodically bump geo ref data cache (services must re-fetch when version changes)
- [ ] Pre-existing svelte-check errors in `geo-ref-data.ts`, `locale-options.ts`, `timezone-options.ts`, `+layout.server.ts`, `debug/design/contact-form/+page.server.ts`, `middleware.mock.server.ts` — caused by proto fields becoming optional. Not introduced by the security tab work; flagged for a separate cleanup pass.
- [ ] Pre-existing TS strictness errors in `auth-tests` for `cache.set.handleAsync.mock.calls[0][0]` accessor patterns and the `update-org-logo`/`update-user-image`/`username-hooks` test files (the matchers `toBeSuccess`/`toBeFailure` augmentation isn't being picked up — likely a separate fix to `auth-tests` tsconfig or a missing `tsconfig.includes` for the setup file).
