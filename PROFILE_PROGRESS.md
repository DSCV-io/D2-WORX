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

- [ ] 3A. Route Group + Layout
- [ ] 3B. Profile Tab (inline edits)
- [ ] 3C. Old Profile Route Redirect
- [ ] 3D. SvelteKit Files API Proxy
- [ ] **Review Checkpoint 3** — profile page renders, edits work

## Phase 4: Security, Sessions, Recent Logins, Danger Zone

- [ ] 4A. Security Tab (change password)
- [ ] 4B. Sessions Tab (list + revoke)
- [ ] 4C. Recent Logins Tab (events + map)
- [ ] 4D. Danger Zone Tab (stubbed)
- [ ] **Review Checkpoint 4** — all tabs functional

## Phase 5: SignalR Integration + Avatar Upload

- [ ] 5A. SignalR Client Infrastructure
- [ ] 5B. Avatar Upload Client
- [ ] 5C. AvatarUploader Component
- [ ] 5D. Wire to Profile Page
- [ ] 5E. SignalR Environment (docker-compose)
- [ ] **Review Checkpoint 5** — avatar upload works E2E

## Phase 6: Navigation Updates

- [ ] 6A. App Header (UserAvatarMenu)
- [ ] 6B. App Sidebar (interactive footer)
- [ ] 6C. Remove old Profile nav link
- [ ] **Review Checkpoint 6** — nav works across all contexts

## Phase 7: Testing + Documentation

- [ ] 7A. Frontend Tests
- [ ] 7B. E2E Tests
- [ ] 7C. Documentation
- [ ] **Review Checkpoint 7 (Final)** — all tests pass, docs updated
