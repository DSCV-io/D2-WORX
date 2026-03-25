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

## Phase 4: SignalR Integration + Avatar Upload + Profile Pic Editor

- [ ] 4A. SignalR Client Infrastructure (@microsoft/signalr, JWT via query param, Svelte 5 runes)
- [ ] 4B. Avatar Upload Client (browser → Files REST API directly, presigned URL → MinIO PUT)
- [ ] 4C. Client-side crop/zoom editor (Canvas or cropperjs, circle frame)
- [ ] 4D. AvatarUploader Component (select → crop → upload → SignalR status → display)
- [ ] 4E. Wire avatar to Profile page
- [ ] **Review Checkpoint 4** — avatar upload works E2E

## Phase 5: Security, Sessions, Recent Logins (full implementation)

- [ ] 5A. Change Password form (current + new + confirm, wired to BetterAuth changePassword)
- [ ] 5B. Active Sessions list (GET /api/account/sessions, UA parsing, per-session revoke)
- [ ] 5C. Recent Logins (paginated sign-in events, Leaflet map with WhoIs locations)
- [ ] 5D. Email & Phone tab — wire email display from user data (phone deferred)
- [ ] 5E. Notification preferences — wire to backend when handlers exist (mock for now)
- [ ] **Review Checkpoint 5** — security/sessions/logins fully functional

**Explicitly deferred:**

- Phone number management (no backend support yet)
- Account deletion (stubbed with toast, no backend support yet)
- Locale/timezone save to backend (no update handlers yet)

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
