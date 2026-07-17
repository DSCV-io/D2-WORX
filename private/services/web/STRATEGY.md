<!--
Copyright (c) DCSV. All rights reserved.
-->

# private/services/web/ — Frontend Strategy

> Library picks, testing strategy, telemetry, and route + component organization for the SvelteKit BFF. Source-of-truth for "what stack are we on" decisions.

---

## Library Picks

The library landscape is stable — these are the canonical picks.

### Forms

- **`sveltekit-superforms`** — primary form library (SPA mode for client-side validation flow)
- **`formsnap`** — accessibility-first form field components built on Superforms
- **`zod` v4** — schema validation (forms + REST client request/response shapes)

Why this stack: Superforms handles SSR + progressive enhancement + validation lifecycle without each form reinventing it. Formsnap layers a11y on top (correct ARIA, error association, label-for). Zod is the validation source of truth — same schemas validate client + server.

### UI Components

- **`shadcn-svelte`** — copy-paste component library (NOT npm-installed; components live in `src/lib/components/ui/`)
- **`bits-ui` v2** — primitives shadcn-svelte builds on (headless, accessible)
- **`tailwindcss` v4** — via Vite plugin, OKLCH theming
- **`@lucide/svelte`** — icons (tree-shakeable, no SVG bundle bloat)
- **`mode-watcher`** — three-way dark/light/system toggle
- **`tailwind-merge` + `clsx`** — className merging utilities (essential for shadcn pattern)
- **`tailwind-variants`** — variant-driven component styling

Why shadcn-svelte (vs Skeleton or other Svelte UI libs): copy-paste is the right model for a long-lived design system — you own the components, you can modify them. Library-as-dep means upgrades can break your UI without warning.

### Toasts

- **`svelte-sonner`** — toast notifications

Sonner pattern (queue + auto-dismiss + accessible focus management). Don't reinvent.

### Charts

- **`layerchart` v2** — primary charting library

LayerChart 2.0 has good Svelte 5 (runes) support + composable primitives. Avoids the Svelte-3-era "wrapper around d3" libraries that became stale.

### Phone & Address Input

- **`libphonenumber-js`** — phone validation + formatting
- **`intl-tel-input`** — international phone input UI (Svelte 5 wrapper authored per integration need)
- **`postcode-validator`** — postal code validation per country
- **`@internationalized/date`** — date input + parsing per locale

Why these: international correctness is hard. These libraries handle the edge cases (country-specific phone formats, postal codes, date conventions per locale) so we don't get bug reports from non-US users.

### Address Autocomplete

Geo service backend concern (frontend calls Edge `/api/v1/geo/...`). No standalone frontend autocomplete library — the Geo client lib + a typeahead component built in-house.

### Data Tables

shadcn-svelte's table component as the base. Don't pull in TanStack Table or similar until concrete need arises (sorting, filtering, virtualization). Premature optimization for our current scale.

---

## Testing Strategy

Two tiers:

### Tier 1 — Vitest (Unit + Component)

- **Vitest** with browser mode for component tests (real Chromium via `@vitest/browser` + `vitest-browser-svelte`)
- **Vitest** Node mode for unit tests (schema validation, helper functions, REST client logic)
- Use custom matchers per [`docs/TESTS.md`](../../docs/TESTS.md) — `toBeSuccess()`, `toHaveErrorCode()`, etc., for D2Result assertions
- File patterns: `*.test.ts` (unit) and `*.svelte.test.ts` (component)

### Tier 2 — Playwright (Mocked)

- **Playwright** with `D2_MOCK_INFRA=true` env var
- All `fetch()` calls intercepted via `page.route()` API
- Server-side data loading (gRPC to Edge) handled via mock layer at SSR
- Shared mock helpers in `tests/fixtures.ts`
- Run in CI with `retries: 1` for flaky async behavior

### Out of scope

- **Local E2E** (real backends) — cross-service E2E is intentionally NOT a tier; real-backend testing is done manually as needed
- **True E2E** — no cross-service test project; correctness is the responsibility of per-service integration tests

### Adversarial Discipline

Per [`docs/TESTS.md`](../../docs/TESTS.md), every form field gets unit + Playwright coverage of the 8-category checklist:

1. Happy path
2. Garbage input (null, empty, whitespace, wrong type, malformed)
3. Boundary values (max length ±1, empty/single/over-cap collections)
4. Format validation (email, phone, URL, date, hex IDs)
5. Cross-field dependencies
6. Error propagation (downstream failures bubble correctly)
7. Idempotency (duplicate submissions safe)
8. Concurrency (race conditions)

Happy-path-only is not enough. If a form field accepts user input, try to break it.

### Other Test Types

Accessibility audits (`axe-core` / `axe-playwright`), visual regression (Playwright screenshots), and performance gating (Lighthouse CI) are OUT OF SCOPE for the current test surface.

---

## Client-Side Telemetry

### Grafana Faro (browser observability)

- **`@grafana/faro-web-sdk`** — error capture, console capture, traces
- **`@grafana/faro-web-tracing`** — auto-instrumented OTel tracing for browser spans
- Traces ship to Tempo via the Alloy `faro.receiver` endpoint (per `infra/observability/alloy/config/config.alloy`)
- Errors ship to Loki (structured)
- Metrics (Web Vitals) ship to Mimir

### Web Vitals (RUM)

Capture and report:

- **TTFB** (Time to First Byte)
- **FCP** (First Contentful Paint)
- **LCP** (Largest Contentful Paint)
- **INP** (Interaction to Next Paint) — replaces FID per Web Vitals 2024 update
- **CLS** (Cumulative Layout Shift)

Dashboard: `infra/observability/grafana/provisioning/dashboards/d2-worx/web-vitals-rum.json`.

### User Identity

**PII rule**: Faro user identity is limited to `userId` + `username`. Never email, real name, contact details, or session payload contents.

### Session Replay

OUT OF SCOPE for the current observability surface. Faro has session replay support (via `@grafana/faro-web-sdk` opt-in), but it's heavy + raises privacy questions — not currently enabled.

---

## Architecture

The route structure + component organization is summarized below.

### Route Groups

| Group           | Purpose                               | Auth Required | Org Required |
| --------------- | ------------------------------------- | ------------- | ------------ |
| `(public)/`     | Marketing, legal, pricing             | No            | No           |
| `(auth)/`       | Sign-in, sign-up, password reset, OTP | No            | No           |
| `(onboarding)/` | Welcome, create/select org            | Yes           | No           |
| `(app)/`        | Main authenticated application        | Yes           | Yes          |

The `(app)/` group is further subdivided by org type: `(customer)/`, `(support)/`, `(admin)/`, `(shared)/` per the multi-org architecture.

### Component Organization

```
src/lib/
  components/
    ui/                  # shadcn-svelte (copy-paste, owned by us)
    forms/               # FormInput, FormTextarea, FormPhoneInput, FormCombobox, etc.
    layout/              # Header, sidebar, mobile nav
    {feature}/           # Feature-specific components (account, files, threads, etc.)
  client/
    auth/                # Browser-side authClient (folder, not a separate package)
    rest/                # REST clients per backend service (edge-client, files-client, etc.)
    stores/              # Svelte stores for app state
  server/
    auth/                # Server-side route guards + session helpers
    middleware/          # Server-side request enrichment, CORS, etc.
  paraglide/             # Generated by Paraglide compile (gitignored)
```

### Browser → Edge Direct (NOT proxied)

The browser talks to Edge DIRECTLY for all auth state mutations:

- Sign-in, sign-up, password reset, OTP → browser POSTs to Edge `/api/v1/auth/*`
- After successful sign-in, Edge sets the cookie; browser doesn't manage cookies
- After sign-out, browser-side `authClient.signOut()` calls Edge to clear server session, then `invalidateAll()` to refetch all `load()` data

SvelteKit does NOT proxy `/api/auth/*`. There is no auth proxy layer in this architecture.

### SSR via SvelteKit + JWT for browser→Edge

- `+page.server.ts` `load()` functions do SSR data fetching via gRPC to Edge (using server-side JWT obtained from session)
- `superForm + SPA: true` for forms — no `+page.server.ts` form actions; submit handled client-side, validated client + server, POSTed to Edge
- Browser direct calls to Edge use JWT (obtained via `authClient.token()`, stored in memory only — never localStorage)

### Defense in Depth (network + application)

For Edge-bypass prevention:

1. **Network-level**: SvelteKit attached only to `internal` overlay network in Swarm; only Edge can reach it
2. **Application-level**: thin hook validates the inbound `Authorization` JWT shape (3 segments, base64url-decodable, JSON-parseable claim object) and the `x-d2-context` envelope shape via the codegen-emitted `PropagatedContextSerializer` — rejects malformed / unsigned requests with RFC 7807 ProblemDetails matching Edge's response shape

### Local Dev

Edge runs alongside SvelteKit. Browser hits Edge on `localhost:5000`, Edge handles `/api/*` in-process and forwards page-render requests to SvelteKit on `localhost:5173`. `make dev` starts everything together. Vite HMR works through Edge's WS upgrade pass-through.

---

## Forms — Complex Interdependencies

For multi-step / cross-field-dependent forms, the pattern is:

1. **Define the full form schema in Zod** at the field level, including cross-field refinements
2. **Use Superforms `superForm()`** to wire the schema to the form
3. **Handle reactive interdependencies via `$derived`** (Svelte 5 runes) — re-derive validation state when dependent fields change
4. **For step transitions**, validate the current step's fields BEFORE allowing next-step navigation (don't trust step state)

### What we still build custom

- **Multi-step wizards** with progress indicator (no Svelte wizard library hits the right ergonomic point)
- **Conditional fields** that appear/disappear based on other field values (Svelte 5 runes handle reactivity well — no library needed)
- **Custom field types** specific to D2 (PhoneInput with country selector, AddressInput with geocoding, etc.) — wrap libphonenumber-js / address autocomplete primitives

### What we DON'T build custom

- Form state machine (Superforms handles)
- Validation lifecycle (Zod + Superforms)
- Server-side action handling (REST clients post to Edge directly, not SvelteKit form actions)
- Accessibility wiring (Formsnap)

---

## Appendix: Library Version Matrix

Baseline reference. Update versions as releases bump.

| Category      | Package                                     | Version         |
| ------------- | ------------------------------------------- | --------------- |
| Framework     | `svelte`                                    | 5.53.5          |
| Framework     | `@sveltejs/kit`                             | 2.53.3          |
| Build         | `vite`                                      | 7.3.1           |
| Styling       | `tailwindcss`                               | 4.2.1           |
| UI            | `bits-ui`                                   | 2.16.2          |
| Icons         | `@lucide/svelte`                            | 0.561.0         |
| Forms         | `sveltekit-superforms`                      | 2.30.0          |
| Forms         | `formsnap`                                  | 2.0.1           |
| Validation    | `zod`                                       | 4.3.6           |
| i18n          | `@inlang/paraglide-js`                      | 2.13.0          |
| Phone         | `libphonenumber-js`                         | 1.12.38         |
| Postal codes  | `postcode-validator`                        | 3.10.9          |
| Dates         | `@internationalized/date`                   | 3.11.0          |
| OTel (server) | `@opentelemetry/sdk-node` + exporters       | 0.212.0         |
| Faro (client) | `@grafana/faro-web-sdk` + tracing           | 1.19.0          |
| Charts        | `layerchart`                                | 2.0.0-next.43   |
| Testing       | `vitest` + `@vitest/browser` + `playwright` | 4.0.18 / 1.58.0 |
| Toast         | `svelte-sonner`                             | 1.0.7           |
| Theme         | `mode-watcher`                              | 1.1.0           |
