<!--
Copyright (c) DCSV. All rights reserved.
-->

# private/services/web/ — SvelteKit BFF

> Parent: [`server/`](../README.md)

> **Status**: BROKEN-BY-DESIGN. The `@d2/*` workspace deps in `package.json` reference packages outside this workspace's resolution graph (this directory is intentionally omitted from `pnpm-workspace.yaml`); `pnpm install` fails on this package. The directory holds the SvelteKit BFF source — a working reference for library picks + structure (workspace re-wire is a tracked future task).
>
> **Strategy reference** (library choices, testing approach): [STRATEGY.md](STRATEGY.md).

---

## What this is

The SvelteKit Backend-for-Frontend:

- **Pure SSR** — `+page.server.ts` `load()` functions for SSR data fetching, `superForm + SPA: true` for forms
- **Browser → Edge directly** for all auth state mutations (no SvelteKit proxy of `/api/auth/*`)
- Server-side route guards live at `src/lib/server/auth/` (folders within `private/services/web/`, not separate packages)
- Browser-side `authClient` lives at `src/lib/client/auth/` (same — folder, not a package)

---

## Durable rules

### Sign-Out Flow

Sign-out must clear ALL auth state in three steps. Missing any step leaves stale state:

```svelte
<script lang="ts">
  import { invalidateAll } from "$app/navigation";
  import { authClient } from "$lib/client/auth/auth-client.js";
  import { invalidateToken } from "$lib/client/rest/edge-client.js";

  async function handleSignOut() {
    await authClient.signOut(); // 1. Clear server session (cookie + Redis + PG via Edge)
    invalidateToken(); // 2. Clear in-memory JWT (prevents stale token reuse)
    await invalidateAll(); // 3. Invalidate SvelteKit data loaders (re-fetch sees no session)
  }
</script>
```

| Step                   | What it clears                     | Without it                                          |
| ---------------------- | ---------------------------------- | --------------------------------------------------- |
| `authClient.signOut()` | Server session (cookie, Redis, PG) | User appears logged in until cookie expires (5 min) |
| `invalidateToken()`    | In-memory JWT in Edge client       | Stale JWT keeps authorizing API calls until expiry  |
| `invalidateAll()`      | SvelteKit layout/page data         | UI shows stale session data until next navigation   |

### Navigation & resolve()

**Always** wrap paths with `resolve()` from `$app/paths` for i18n locale routing:

```svelte
<!-- CORRECT — works with all locales -->
<a href={resolve("/dashboard")}>Dashboard</a>
<button onclick={() => goto(resolve("/settings"))}>Settings</button>

<!-- WRONG — breaks for non-default locales (e.g., /de/dashboard) -->
<a href="/dashboard">Dashboard</a>
<button onclick={() => goto("/settings")}>Settings</button>
```

For paths with query strings, append after `resolve()`:

```svelte
<a href={`${resolve("/search")}?q=${query}`}>Search</a>
```

This applies to ALL navigation: `<a href>`, `goto()`, `redirect()`, and `fetch()` calls to SvelteKit routes.

### REST clients own all fetch calls

Never use raw `fetch` outside of `*-client.ts` files in `$lib/client/rest/`. Components and pages call client functions (`updateName()`, `removeAvatar()`, etc.), never `fetch("/api/...")` directly. Clients handle headers (CSRF `Content-Type`, custom `X-D2-*`), credentials, timeouts, and D2Result parsing in one place.

### Skeleton loading states

Every component that displays async or server-loaded data must show a `<Skeleton>` placeholder until the data is ready. Use a `loaded` flag (set by `$effect` after data sync) rather than checking `!user` (which is never null with SSR). For async resources like presigned URLs, initialize the loading flag from the data: `let avatarLoading = $state(!!user.image)` so SSR renders the skeleton immediately.

### Avatar `{#key}` blocks

shadcn's `Avatar.Root` caches internal "image loaded" state. When the image URL changes (upload, remove), wrap `Avatar.Root` in `{#key url}` to force re-mount so the Fallback renders correctly. Apply to all avatar locations (profile uploader, header menu, mobile nav).

### Client-side telemetry must never include PII

Faro user identity is limited to `userId` + `username`. Never email, real name, or contact details.

### i18n everywhere

ALL user-visible strings MUST use Paraglide translations (`m.key_name()` from `$lib/paraglide/messages.js`). Includes `<title>`, meta tags, OG tags, headings, labels, placeholders, error messages. Never hardcode — not even for dev/debug pages.

New pages MUST include in `<svelte:head>`: translated `<title>`, `<meta name="description">`, OG tags (`og:title`, `og:description`, `og:type="website"`), `noindex` if not indexable.

When adding translation keys: add to ALL locale files in `contracts/messages/` simultaneously.

---

## Testing

Two tiers:

| Tier                  | Tool                                 | Purpose                                |
| --------------------- | ------------------------------------ | -------------------------------------- |
| **Unit / component**  | Vitest browser mode                  | Component behavior + schema validation |
| **Mocked Playwright** | Playwright with `D2_MOCK_INFRA=true` | Real browser, all `fetch()` mocked     |

Cross-service E2E is intentionally NOT in scope — anything requiring multiple services running is done manually as needed. Adversarial test discipline per [`docs/TESTS.md`](../../docs/TESTS.md) — happy path is not enough. Every form field gets unit + Playwright coverage of the 8-category checklist.

---

## Strategy doc

Library choices + testing approach: [STRATEGY.md](STRATEGY.md). Library recommendations there (Superforms + Formsnap + Zod 4, shadcn-svelte + Bits UI, Sonner toasts, LayerChart 2.0, etc.) are the canonical picks for the SvelteKit BFF.
