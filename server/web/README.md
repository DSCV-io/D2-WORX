# D2-WORX Web Client

## Overview

The **Web Client** is a SvelteKit 5 application that serves as the primary user interface for D2-WORX. It provides SSR-capable pages, BetterAuth session management (cookie-based), and communicates with backend services via the .NET REST Gateway using JWT authentication.

**Runtime:** Node.js (SvelteKit + adapter-node)
**Framework:** Svelte 5 (runes) + SvelteKit 2
**Styling:** Tailwind CSS v4 (Vite plugin, OKLCH theming with 3 presets)
**i18n:** Paraglide (10 BCP 47 locales: en-US, en-GB, en-CA, es-ES, es-MX, de-DE, fr-FR, fr-CA, it-IT, ja-JP)
**Observability:** OpenTelemetry (server-side), Grafana Faro (client-side errors → Loki, traces → Tempo, Web Vitals → Mimir)

> **Not a standalone app.** The web client depends on the .NET REST Gateway for all data operations and on the Auth Service (proxied through SvelteKit) for authentication. It is orchestrated via Docker Compose alongside all backend services.

---

## Architecture

### Request Flow (Hybrid Pattern C)

Two coexisting paths — SSR through SvelteKit server, interactive calls direct to gateway:

```
SSR / slow-changing data:
  Browser ──cookie──► SvelteKit Server ──JWT──► .NET Gateway ──gRPC──► Services

Interactive client-side (search, forms, real-time):
  Browser ──JWT──► .NET Gateway ──gRPC──► Services

Auth (always proxied, cookie-based):
  Browser ──cookie──► SvelteKit ──proxy──► Auth Service
```

### Auth Integration

- **BetterAuth** proxied through SvelteKit (`/api/auth/*` catch-all)
- **Sessions:** Cookie-based between Browser ↔ SvelteKit (httpOnly, secure)
- **JWTs:** RS256, 15-minute expiry, obtained via `authClient.token()`, stored in memory only (never localStorage)
- **Route protection:** Server-side in `hooks.server.ts` (no client-side flash of unauthenticated content)

### Route Groups

| Group           | Purpose                          | Auth Required | Org Required |
| --------------- | -------------------------------- | ------------- | ------------ |
| `(public)/`     | Marketing, legal, pricing        | No            | No           |
| `(auth)/`       | Sign-in, sign-up, password reset | No            | No           |
| `(onboarding)/` | Welcome, create/select org       | Yes           | No           |
| `(app)/`        | Main authenticated application   | Yes           | Yes          |

The `(app)/` group is further subdivided by org type: `(customer)/`, `(support)/`, `(admin)/`, `(shared)/`.

### Server Middleware Pipeline

Runs on every request via `hooks.server.ts`:

1. **Request Enrichment** — IP resolution, client/device fingerprint, WhoIs geolocation via Geo gRPC
2. **Rate Limiting** — Multi-dimensional sliding window (device, IP, city, country) via Redis
3. **Auth Session Resolution** — Cookie → Auth Service via `@d2/auth-bff-client`
4. **Idempotency** — `Idempotency-Key` header deduplication via Redis
5. **Paraglide i18n** — Locale detection + URL rerouting
6. **Request Logging** — Structured logs with OTel trace correlation

### Client-Side Features

- **Gateway Client** — `apiCall()` (authenticated, JWT auto-refresh) and `apiCallAnon()` (public)
- **Device Fingerprint** — Client FP generator, `d2-cfp` cookie, `X-Client-Fingerprint` header
- **Faro Telemetry** — Browser errors, console capture, traces, Web Vitals (TTFB, FCP, LCP, INP, CLS)
- **User Enrichment** — Faro user identity set/cleared reactively from session state

---

## Documentation Index

| Document                                           | Description                                                                                                 |
| -------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| [`SVELTEKIT_STRATEGY.md`](SVELTEKIT_STRATEGY.md)   | Comprehensive strategy report: old system analysis, library recommendations, testing, client-side telemetry |
| [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) | Step-by-step implementation plan with progress tracker                                                      |

---

## What's In Place

### Core Infrastructure

- SvelteKit 2.53.3 with Svelte 5.53.5 (runes), adapter-node, Vite 7.3.1
- Tailwind CSS v4.2.1 via Vite plugin with OKLCH theming (3 presets: WORX, Zinc, Ocean)
- Paraglide i18n with 10 BCP 47 locales (`PUBLIC_ENABLED_LOCALES__N` env vars), server middleware URL rerouting
- mdsvex preprocessor for `.svx` markdown-in-Svelte files
- Docker Compose orchestration with Vite dev server (hot reload via bind mounts)

### Auth & Session

- BetterAuth proxy (`/api/auth/*` catch-all) with cookie session management
- `@d2/auth-bff-client` integration: SessionResolver, JwtManager, AuthProxy, route guards
- Route guards: `requireAuth()`, `requireOrg()`, `redirectIfAuthenticated()`
- Session + user data in `event.locals` (typed in `app.d.ts`)
- JWT auto-refresh with 2-minute buffer, in-memory only (XSS safe)

### UI Components & Design System

- shadcn-svelte (42 component categories) built on Bits UI v2.16.2
- Live theme editor at `/design` with 3 OKLCH presets
- Dark + Light + System three-way toggle (mode-watcher)
- Gabarito font, Lucide icons (tree-shakeable)
- LayerChart 2.0 showcase (area, bar, line, donut, sparkline)

### Forms

- Superforms v2.30.0 + Formsnap + Zod 4 (73 unit tests)
- 7 form field components: `FormInput`, `FormTextarea`, `FormCheckbox`, `FormSelect`, `FormCombobox`, `FormPhoneInput`, `FormSwitch`
- Field presets with validation: NAME, EMAIL, PASSWORD (min 12, max 128)
- Cross-field validation (email confirmation, password confirmation)
- Geo reference data integration (countries, locales) via geo-client gRPC

### Auth Pages

- Sign-in, sign-up, forgot-password, reset-password, verify-email
- i18n support (10 BCP 47 locales), auth-aware public nav, TextLink component

### Observability

- **Server:** Full OTel (traces, logs, metrics) via OTLP/HTTP to Alloy
- **Client:** Grafana Faro (errors → Loki, traces → Tempo, Web Vitals → Mimir)
- Structured server logger with OTel trace correlation
- Web Vitals RUM dashboard in Grafana

### Testing

- Vitest 3.2.4 with dual-project setup (browser + server)
- vitest-browser-svelte for component tests (real Chromium)
- Three-tier Playwright test architecture (mocked CI, local E2E, cross-service E2E)
- Error page with traceId display

### Sign-Out Flow

Sign-out must clear ALL auth state in three steps. Missing any step leaves stale state:

```svelte
<script lang="ts">
  import { invalidateAll } from "$app/navigation";
  import { authClient } from "$lib/client/stores/auth-client.js";
  import { invalidateToken } from "$lib/client/rest/gateway-client.js";

  async function handleSignOut() {
    await authClient.signOut(); // 1. Clear server session (cookie + Redis + PG)
    invalidateToken(); // 2. Clear in-memory JWT (prevents stale token reuse)
    await invalidateAll(); // 3. Invalidate SvelteKit data loaders (re-fetch sees no session)
  }
</script>
```

| Step                   | What it clears                     | Without it                                          |
| ---------------------- | ---------------------------------- | --------------------------------------------------- |
| `authClient.signOut()` | Server session (cookie, Redis, PG) | User appears logged in until cookie expires (5 min) |
| `invalidateToken()`    | In-memory JWT in gateway client    | Stale JWT keeps authorizing API calls until expiry  |
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

---

## What's Not Yet In Place

- **Onboarding flow** — Org selection/creation pages (layouts ready, pages not built)
- **App shell finalization** — Org-type nav sharding, org switcher, emulation banner
- **SignalR integration** — Browser → .NET gateway direct (`@microsoft/signalr`)
- **Phone input wrapper** — `intl-tel-input` Svelte 5 component (libphonenumber-js installed for validation)
- **Address autocomplete** — Radar as Geo service backend concern (frontend calls gateway)
- **Data tables** — shadcn table available but not used in real routes yet
- **A11y testing** — axe-core packages installed, not integrated into suite
- **Performance testing** — Lighthouse CI installed, not configured

---

## Tech Stack

### Installed Dependencies

| Category      | Package                                     | Version        |
| ------------- | ------------------------------------------- | -------------- |
| Framework     | `svelte`                                    | 5.53.5         |
| Framework     | `@sveltejs/kit`                             | 2.53.3         |
| Build         | `vite`                                      | 7.3.1          |
| Styling       | `tailwindcss`                               | 4.2.1          |
| Styling       | `clsx` + `tailwind-merge`                   | 2.1.1 / 3.5.0  |
| Styling       | `tailwind-variants`                         | 3.2.2          |
| UI            | `bits-ui` (shadcn-svelte)                   | 2.16.2         |
| Icons         | `@lucide/svelte`                            | 0.561.0        |
| Forms         | `sveltekit-superforms` + `formsnap`         | 2.30.0 / 2.0.1 |
| Validation    | `zod`                                       | 4.3.6          |
| i18n          | `@inlang/paraglide-js`                      | 2.13.0         |
| Phone         | `libphonenumber-js`                         | 1.12.38        |
| Postal codes  | `postcode-validator`                        | 3.10.9         |
| Dates         | `@internationalized/date`                   | 3.11.0         |
| Auth          | `better-auth` + `@d2/auth-bff-client`       | 1.5.0          |
| Payments      | `@stripe/stripe-js`                         | 8.8.0          |
| OTel (server) | `@opentelemetry/sdk-node` + exporters       | 0.212.0        |
| Faro (client) | `@grafana/faro-web-sdk` + tracing           | 1.19.0         |
| Charts        | `layerchart`                                | 2.0.0-next.43  |
| Testing       | `vitest` + `@vitest/browser` + `playwright` | 3.2.4 / 1.58.0 |
| Toast         | `svelte-sonner`                             | 1.0.7          |
| Theme         | `mode-watcher`                              | 1.1.0          |

### Workspace Dependencies (`@d2/*`)

| Package                  | Purpose                                            |
| ------------------------ | -------------------------------------------------- |
| `@d2/auth-bff-client`    | BFF auth proxy, session resolution, JWT management |
| `@d2/geo-client`         | Geo service gRPC client (FindWhoIs, ref data)      |
| `@d2/request-enrichment` | IP, fingerprint, WhoIs middleware                  |
| `@d2/ratelimit`          | Multi-dimensional rate limiting                    |
| `@d2/idempotency`        | Idempotency-Key header middleware                  |
| `@d2/cache-memory`       | In-memory cache (LRU)                              |
| `@d2/cache-redis`        | Redis distributed cache                            |
| `@d2/handler`            | BaseHandler pattern                                |
| `@d2/interfaces`         | Cache + middleware contracts                       |
| `@d2/logging`            | Structured Pino logger (OTel-instrumented)         |
| `@d2/service-defaults`   | OTel SDK bootstrap                                 |
| `@d2/protos`             | Generated gRPC types                               |
| `@d2/result`             | D2Result pattern                                   |

---

## Development

### Prerequisites

- Node.js 24+
- pnpm 10+
- All backend services running via Docker Compose (`docker compose --env-file .env.local up -d`)

### Commands

```bash
pnpm dev              # Start dev server (port 5173)
pnpm build            # Production build
pnpm preview          # Preview production build
pnpm check            # Type checking (svelte-check)
pnpm test:unit        # Vitest (component + server tests)
pnpm test:e2e:mocked  # Playwright mocked tests (CI-safe, no backends needed)
pnpm test:e2e:local   # Playwright local E2E (requires all services running)
pnpm test             # All tests (unit + mocked E2E)
```

### Environment Variables

| Variable                      | Purpose                          | Required                     |
| ----------------------------- | -------------------------------- | ---------------------------- |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP collector endpoint          | For observability            |
| `OTEL_SERVICE_NAME`           | Service name for traces/logs     | For observability            |
| `PUBLIC_FARO_COLLECTOR_URL`   | Faro collector URL (client-side) | For client telemetry         |
| `PUBLIC_GATEWAY_URL`          | .NET Gateway URL (client-side)   | For client API calls         |
| `SVELTEKIT_AUTH__URL`         | Auth service URL (server-side)   | For auth proxy               |
| `GATEWAY_URL`                 | .NET Gateway URL (server-side)   | For SSR API calls            |
| `PUBLIC_DEFAULT_LOCALE`       | Default BCP 47 locale            | For i18n (defaults to en-US) |
| `PUBLIC_ENABLED_LOCALES__N`   | Indexed BCP 47 locale list       | For i18n (defaults to en-US) |
| `D2_MOCK_INFRA`               | Enable mock infra for tests      | For mocked E2E tests         |

See `.env.local.example` in the project root for all environment variables.

---

## Testing Strategy

### Unit & Component Tests

| Level     | Tool                  | File Pattern       | Environment        |
| --------- | --------------------- | ------------------ | ------------------ |
| Unit      | Vitest (Node)         | `*.test.ts`        | Node               |
| Component | vitest-browser-svelte | `*.svelte.test.ts` | Browser (Chromium) |

### Playwright Tests (Three-Tier Architecture)

E2E tests are split into three tiers based on infrastructure requirements:

| Tier          | Directory                     | Config                       | Script                 | Backends Required | CI  |
| ------------- | ----------------------------- | ---------------------------- | ---------------------- | ----------------- | --- |
| **Mocked**    | `tests/mocked/`               | `playwright.config.ts`       | `pnpm test:e2e:mocked` | None              | Yes |
| **Local E2E** | `tests/e2e/`                  | `playwright.local.config.ts` | `pnpm test:e2e:local`  | All (Compose)     | No  |
| **True E2E**  | `backends/node/services/e2e/` | (separate project)           | (separate project)     | All (Compose)     | No  |

**Tier 1 — Mocked tests** (`tests/mocked/*.spec.ts`): Run against the SvelteKit dev server with `D2_MOCK_INFRA=true`. Client-side HTTP requests are intercepted via Playwright's `page.route()` API. Server-side data loading (e.g., geo ref data via gRPC) is handled by the `D2_MOCK_INFRA` env var, which provides mock data at the SSR level. Shared mock helpers live in `tests/mocked/fixtures.ts` (e.g., `mockEmailCheck`). These tests run in CI and use `retries: 1` for flaky async behavior.

**Tier 2 — Local E2E** (`tests/e2e/*.spec.ts`): Full integration tests that require all backend services running (via Docker Compose). Uses `playwright.local.config.ts` which reuses an existing dev server on port 5173. Not included in CI — local development only.

**Tier 3 — True E2E** (`backends/node/services/e2e/`): Cross-service E2E tests in a separate project. Tests the full stack including Auth, Comms, and Geo services. Managed independently from the web client.

### Other Test Types

| Level         | Tool                   | Status         |
| ------------- | ---------------------- | -------------- |
| Accessibility | axe-core/playwright    | Not integrated |
| Visual        | Playwright screenshots | In E2E suite   |
| Performance   | Lighthouse CI          | Not configured |

See [`SVELTEKIT_STRATEGY.md` §7](SVELTEKIT_STRATEGY.md#7-frontend-testing-strategy) for full testing architecture.
