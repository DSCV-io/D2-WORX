# SvelteKit Strategy Report — D2-WORX Web Client

Deep analysis of the old DeCAF SvelteKit project, what to carry forward, what to change, and library recommendations for the new D2-WORX architecture.

---

## Table of Contents

1. [Old System Analysis](#1-old-system-analysis)
   - [Routing & Auth Guards](#11-routing--auth-guards)
   - [Form System](#12-form-system)
   - [Component Library](#13-component-library)
   - [API Layer](#14-api-layer)
   - [State Management](#15-state-management)
   - [Styling & Theming](#16-styling--theming)
   - [Internationalization](#17-internationalization)
2. [What Changed: DeCAF → D2-WORX](#2-what-changed-decaf--d2-worx)
3. [Library Recommendations](#3-library-recommendations)
   - [Forms](#31-forms)
   - [UI Components](#32-ui-components)
   - [Address Autocomplete](#33-address-autocomplete)
   - [Phone Input](#34-phone-input)
   - [Toasts](#35-toasts)
   - [Data Tables](#36-data-tables)
   - [Date/Time](#37-datetime)
4. [Auth Integration Strategy](#4-auth-integration-strategy)
5. [Form System: Old vs. New](#5-form-system-old-vs-new)
6. [Proposed Architecture](#6-proposed-architecture)
7. [Frontend Testing Strategy](#7-frontend-testing-strategy)
   - [Component Testing](#71-component-testing)
   - [E2E Testing with Playwright](#72-e2e-testing-with-playwright)
   - [Accessibility Testing](#73-accessibility-testing)
   - [Visual Regression Testing](#74-visual-regression-testing)
   - [Performance Testing](#75-performance-testing)
   - [Test Organization](#76-test-organization)
8. [Client-Side Telemetry & Error Monitoring](#8-client-side-telemetry--error-monitoring)
   - [Error Capture Strategy](#81-error-capture-strategy)
   - [Browser Observability (Grafana Faro)](#82-browser-observability-grafana-faro)
   - [Web Vitals (RUM)](#83-web-vitals-rum)
   - [Session Replay](#84-session-replay)
   - [What's Missing Today](#85-whats-missing-today)

---

## 1. Old System Analysis

Source: `old/DeCAF-DCSV/FE_Svelte/`

### 1.1 Routing & Auth Guards

**Route groups:**

| Group        | Purpose                                                 | Guard                                       |
| ------------ | ------------------------------------------------------- | ------------------------------------------- |
| `(public)/`  | Sign-in, sign-up, forgot password, pricing, legal pages | None (Vanta.js fog background, PublicNav)   |
| `(private)/` | Dashboard, org management, user profile, admin/CSR      | `+layout.ts` redirect if `!isAuthenticated` |

**Auth check pattern** — root `+layout.ts` calls `checkAuth({ url })` on every navigation. This is **client-side only** (`if (!browser) return`), hitting `GET /auth/current-session` each time. No caching. No server-side session resolution.

**Org guard** — `dashboard/+layout.ts` checks `hasOrgs` and redirects to `/organization/landing` if false. This is the "no-org onboarding" pattern D2-WORX preserves.

**Problems:**

- Auth check is client-only → flash of unauthenticated content on SSR, no server-side protection
- API call on every navigation → unnecessary latency, no session caching
- Client-side `+layout.svelte` fallback shows a 5-second countdown timer before redirect (poor UX)
- Route protection logic split between `+layout.ts` (server-like) and `+layout.svelte` (client fallback)

**What to keep:**

- Route group concept (public vs. private)
- Org landing/selection flow (landing → select → new org → dashboard)
- Return-to parameter on sign-in redirect

### 1.2 Form System

The most architecturally significant part of the old codebase. Built entirely custom.

#### Core Architecture

**`DFormInput` class** (form-types.ts) — central model for every form field:

| Property           | Type                                       | Purpose                                          |
| ------------------ | ------------------------------------------ | ------------------------------------------------ |
| `label`            | `string`                                   | Display label                                    |
| `placeholder`      | `string`                                   | Input placeholder                                |
| `type`             | `string`                                   | HTML input type                                  |
| `required`         | `boolean`                                  | Required flag                                    |
| `value`            | `unknown`                                  | Current value                                    |
| `schema`           | `z.ZodType`                                | Zod validation schema                            |
| `apiName`          | `string`                                   | Maps to server-side field name for error mapping |
| `instructions`     | `string \| null`                           | Help text below input                            |
| `icon`             | `string \| null`                           | FontAwesome icon class                           |
| `customValidation` | `(value, field, ctx) => Promise<string[]>` | Async validation (e.g., email uniqueness check)  |
| `postValidation`   | `(value, field, ctx) => Promise<void>`     | Cross-field validation (e.g., password match)    |
| `onFilter`         | `(value, field, ctx) => Promise<unknown>`  | Input masking/filtering                          |
| `options`          | `DFormSelectOption[]`                      | Dropdown options                                 |
| `errors`           | `string[]`                                 | Current validation errors                        |
| `touched`          | `boolean`                                  | Interaction tracking                             |
| `loading`          | `boolean`                                  | Async validation in progress                     |
| `validate`         | `{ ctr: number }`                          | Counter-based validation trigger                 |

**`DFormContext`** — distributed via `setContext`/`getContext`:

```typescript
type DFormContext = {
  form: SvelteMap<string, DFormInput>;
  update: (map: SvelteMap<string, DFormInput>) => void;
  updateField: (key: string, value: unknown) => void;
};
```

**`PremadeInputs`** — static factory with presets: `email()`, `firstName()`, `lastName()`, `password()`, `confirmPassword()`, `country()`, `region()`, `city()`, `postalCode()`, `streetLine1/2/3()`, `phone()`, `select()`, `orgSelect()`, `website()`, `username()`, `companyName()`, `companyPosition()`, `agreeToPrivacyAndTerms()`.

#### What Works Well

1. **PremadeInputs factory** — centralized field definitions prevent inconsistency. New forms are trivial to scaffold. Sensible defaults for validation, icons, placeholders, filtering.

2. **Zod integration** — each field carries its own schema. Type-safe, composable. The three-hook pipeline (`onFilter` → `customValidation` → Zod → `postValidation`) covers every real-world validation scenario.

3. **Server error mapping** — `getAndSetErrorsFromRes()` maps backend validation errors back to the correct form field via `apiName`. Essential for any real app.

4. **Input filtering/masking** — `onFilter` handles email lowercase + special char stripping, phone digits-only, postal code uppercase. Clean separation of display formatting from stored value.

5. **Cross-field validation** — `postValidation` enables patterns like password-confirm matching that need to inspect multiple fields. Runs after individual field validation.

6. **Dependent options** — `SelectInput` supports `dependentOptions` and `setDependentOptionsBasedOnTextValueFunction` for cascading selects (country → region) and remote search with 200ms debounce.

7. **Address autocomplete** — `AddressInput` integrates autocomplete search (1000ms debounce, 4-char minimum) with full address population across multiple form fields on selection.

#### What Needs Improvement

**Problem 1: Massive code duplication (~50 identical lines per component)**

Every input component (`TextInput`, `SelectInput`, `PhoneInput`, `CheckboxInput`, `AddressInput`) repeats the same block:

```typescript
// These ~50 lines are IDENTICAL in every input component:
let error = $state("");
let touched = $state(false);
let loading = $state(false);
let showError = $derived(touched && !!error);
// ... 8 more state declarations

$effect(() => {
  error = formInput.error;
  touched = formInput.touched;
  // ... sync from DFormInput → local state
});

function doValidate() {
  // identical validation logic in every component
}
```

When validation behavior needs to change, you touch 5+ files. Bugs in one component's copy go unnoticed in others.

**Problem 2: Counter-increment + setTimeout polling for form validation**

```typescript
// validateForm() increments every field's validate.ctr
formInput.validate.ctr++;

// Each component watches the counter in $effect and triggers validation
$effect(() => {
  if (formInput.validate.ctr > 0) doValidate();
});

// Then validateForm() polls every 100ms until no fields are loading
const interval = setInterval(() => {
  if (![...form.values()].some((f) => f.loading)) {
    clearInterval(interval);
    resolve();
  }
}, 100);
```

This is a race condition waiting to happen. The 100ms polling is fragile, and the counter pattern is a workaround for not having a proper event/callback system.

**Problem 3: 12-parameter positional constructor**

```typescript
new DFormInput("Email", "you@example.com", "email", true, "", z.string().email(), "email", ...)
```

Easy to swap `apiName` and `instructions` by accident. An options object would be much safer.

**Problem 4: Bidirectional state sync**

`DFormInput` holds canonical state, but each component also has local `$state` copies synced via `$effect`. Changes flow DFormInput → component via effect, and component → DFormInput via direct mutation. Hard to reason about.

**Problem 5: No progressive enhancement**

All form handling is client-side. No SvelteKit form actions, no `use:enhance`, no server-side validation. If JS fails to load, forms don't work at all.

### 1.3 Component Library

#### UI Primitives

| Component              | Lines | Notes                                                                        |
| ---------------------- | ----- | ---------------------------------------------------------------------------- |
| `Button.svelte`        | ~120  | 7 style types, loading state, href support, onClick with auto-loading        |
| `Modal.svelte`         | ~80   | Native `<dialog>`, 3 sizes, snippet-based header/footer, click-outside-close |
| `Title.svelte`         | ~20   | Page title with FA icon                                                      |
| `Tooltip.svelte`       | ~15   | CSS-only (group-hover), positioned above target                              |
| `Alert.svelte`         | ~30   | 3 types (info/warning/error), dashed border                                  |
| `Divider.svelte`       | ~10   | `<hr>` with optional centered text                                           |
| `ImageCarousel.svelte` | ~80   | Primary + thumbnails, lightbox modal                                         |

**Assessment:** Functional but minimal. Button is well-designed with good loading state management. Modal uses native `<dialog>` (good). No dropdown, no popover, no tabs, no accordion — these were handled inline in domain components rather than as reusable primitives.

#### Table Components

A shadcn-style composable table: `Table`, `TableBody`, `TableCaption`, `TableCell`, `TableFooter`, `TableHead`, `TableHeader`, `TableRow`. **Uses Svelte 4 syntax** (`export let`, `<slot />`, `$$restProps`) — never migrated to Svelte 5.

#### Navigation

- **PublicNav** — responsive with sale banner, mega menu, desktop/mobile variants
- **AccountNavDesktop** — collapsible sidebar with gradient header, nav links, expand/collapse toggle
- **AccountNavMobile** — stub (just a header)
- **MegaMenu** — slide-down overlay with sitemap, mouseleave auto-close

#### Domain-Specific

- **CreateOrgForm** — full org creation with address autocomplete, phone with country picker, country/region selects
- **SelectOrgForm** — org picker with image, type display, routing by org type
- **CheckoutForm** — multi-step wizard (Billing → Method → Gift? → Review) with Stripe Elements
- **PaymentMethodCard** — credit card/bank account preview with CRUD
- **SelectPackageForm** — subscription package selection

**Assessment:** The domain components are well-built and represent significant work. The lack of reusable primitives (dropdown, popover, combobox) meant these were built ad-hoc in each domain component, leading to inconsistent behavior.

### 1.4 API Layer

**`DeCAFBE`** — singleton HTTP client wrapping `fetch`:

```typescript
class DeCAFBE {
    private static instance: DeCAFBE;
    static getInstance(): DeCAFBE { ... }

    async get<T>(path: string, opts?): Promise<DeCAFResult<T>> { ... }
    async post<T>(path: string, body: unknown, opts?): Promise<DeCAFResult<T>> { ... }
    async put<T>(...): Promise<DeCAFResult<T>> { ... }
    async patch<T>(...): Promise<DeCAFResult<T>> { ... }
    async delete<T>(...): Promise<DeCAFResult<T>> { ... }
    async patchWithFile<T>(...): Promise<DeCAFResult<T>> { ... }
}
```

**`DeCAFResult<T>`** — mirrors backend result pattern:

```typescript
interface DeCAFResult<T> {
  messages: string[];
  result: T;
  success: boolean;
  statusCode: string;
  errors?: [string[]]; // Server validation errors: [[propName, ...messages]]
}
```

**Characteristics:**

- `credentials: "include"` on all requests (cookie-based auth)
- Auto-handles 401 (redirect to sign-in with return path)
- Toast integration for error display (commented out)
- File upload detection (auto-switches to FormData)
- Base64 credential encoding (provides no security benefit)

**Problems:**

- Singleton used from both server (`hooks.server.ts`) and client — `credentials: "include"` only works in browser
- No request cancellation / AbortController
- No retry logic
- No request deduplication
- Base URL from `$env/dynamic/public` means it's always the same regardless of server/client context

### 1.5 State Management

**Patterns used:**

1. **Svelte 5 runes** — `$state()`, `$derived()`, `$effect()`, `$props()` throughout
2. **SvelteMap** — reactive Map for form state (fine-grained reactivity)
3. **Svelte context** — `setContext`/`getContext` for form state distribution
4. **Prop drilling** — layout data flows down via `data` props
5. **Singleton** — `DeCAFBE.getInstance()` for API client

**No global store.** Auth state is fetched fresh on every page load. No centralized state management beyond form contexts.

**Assessment:** The runes-first approach is correct for Svelte 5. The lack of a global store is fine for the old monolithic architecture but will need revisiting for D2-WORX's split auth model.

### 1.6 Styling & Theming

**Tailwind CSS v4.1** with HSL-based design tokens in `app.css`:

| Token           | Value           | Visual              |
| --------------- | --------------- | ------------------- |
| `--background`  | `197, 50%, 10%` | Very dark teal-blue |
| `--foreground`  | `205 40% 85%`   | Light blue-gray     |
| `--primary`     | `177 100% 40%`  | Bright teal/cyan    |
| `--secondary`   | `14 100% 63.1%` | Coral/orange        |
| `--destructive` | `340 75% 60%`   | Pink-red            |
| `--muted`       | `205 20% 20%`   | Dark gray-blue      |
| `--ring`        | `291, 27%, 83%` | Light purple        |

**Dark-only theme.** No light mode. Font: Gabarito (Google Fonts CDN). Icons: FontAwesome Kit CDN.

**Utilities:**

- `cn()` — `clsx` + `tailwind-merge` (standard pattern)
- `tailwind-variants` installed but unused
- `tailwindcss-motion` for animations
- Custom CSS: card hover effects, text shadows, scrollbar, heading styles

**Assessment:** The token system is solid and matches shadcn-svelte's convention (HSL-based CSS custom properties). Should carry forward.

### 1.7 Internationalization

**None.** All strings hardcoded in English. The new D2-WORX app already has Paraglide set up with 5 locales (en, es, de, fr, jp) — this is a significant improvement.

---

## 2. What Changed: DeCAF → D2-WORX

The architectural shift from DeCAF (monolith) to D2-WORX (microservices) introduces several concerns that affect the SvelteKit client:

| Concern           | DeCAF                               | D2-WORX                                                   | Impact on SvelteKit                                                         |
| ----------------- | ----------------------------------- | --------------------------------------------------------- | --------------------------------------------------------------------------- |
| **Auth**          | Single backend with session cookies | BetterAuth (standalone service) + proxy through SvelteKit | Auth calls proxied, JWT for gateway calls                                   |
| **API gateway**   | Direct to monolith                  | .NET REST Gateway → gRPC services                         | Two API paths: SvelteKit server → Gateway (SSR), Browser → Gateway (client) |
| **Session model** | Simple cookie session               | 3-tier (cookie cache → Redis → PG) + JWT for services     | Client needs both session cookie AND JWT lifecycle                          |
| **Org context**   | Session-level                       | JWT claim (orgId, orgType, role) + session fields         | JWT refresh on org switch, session field updates                            |
| **Geo data**      | Backend prefetches on every request | Separate Geo service, cached heavily                      | Need geo-client or gateway endpoints for countries/regions/autocomplete     |
| **Rate limiting** | None (or basic)                     | Multi-dimensional (fingerprint, IP, city, country)        | Client must send `X-Client-Fingerprint` header                              |
| **Idempotency**   | None                                | `Idempotency-Key` header on mutations                     | Client must generate + send idempotency keys                                |
| **i18n**          | None                                | Paraglide (5 locales)                                     | All user-facing strings through message functions                           |
| **Observability** | None                                | Full OTel (traces, logs, metrics)                         | Server + client instrumentation                                             |
| **Notifications** | Direct email/SMS                    | Comms service via RabbitMQ, contactId resolution          | No direct email/SMS from SvelteKit                                          |

### BetterAuth Proxy Pattern Impact

SvelteKit acts as the BetterAuth host for browser clients. This means:

1. **All auth API calls** (`/api/auth/*`) go through SvelteKit's server, not directly to the Auth service
2. **Session cookies** are between Browser ↔ SvelteKit (httpOnly, secure)
3. **JWT tokens** are obtained via `authClient.token()` (which proxies through SvelteKit to Auth service)
4. **Server-side rendering** can access the session directly in `hooks.server.ts` and populate `event.locals`
5. **Client-side** uses the reactive `authClient.useSession()` store

This is fundamentally different from DeCAF's "call the backend directly with cookies" model. The SvelteKit server becomes an active participant in the auth flow, not just a static file server.

### Dual API Path (Hybrid Pattern C)

```
SSR / slow-changing data:
  Browser ──cookie──► SvelteKit Server ──JWT──► .NET Gateway ──gRPC──► Services

Interactive client-side (search, forms, real-time):
  Browser ──JWT──► .NET Gateway ──gRPC──► Services

Auth (always proxied, cookie-based):
  Browser ──cookie──► SvelteKit ──proxy──► Auth Service
```

The SvelteKit API client layer needs to handle BOTH paths:

- **Server-side:** Obtain JWT from auth service, call gateway with `Authorization: Bearer` header
- **Client-side:** Obtain JWT via `authClient.token()`, call gateway directly with `Authorization: Bearer` header
- **Auth:** Always through SvelteKit proxy (cookie-based, no JWT needed)

---

## 3. Library Recommendations

### 3.1 Forms

#### Recommendation: **Superforms** + Formsnap (via shadcn-svelte)

| Library                                          | Version | Stars  | Svelte 5         | Status             |
| ------------------------------------------------ | ------- | ------ | ---------------- | ------------------ |
| [sveltekit-superforms](https://superforms.rocks) | 2.29.1  | ~2,700 | Yes (runes mode) | Active, mature     |
| [formsnap](https://formsnap.dev)                 | 1.0.1   | ~700   | Yes              | Active (v2 in dev) |

**Why Superforms over custom:**

1. **Progressive enhancement** — works without JS via SvelteKit form actions + `use:enhance`. DeCAF's forms were JS-only.
2. **Server-side validation** — Zod schema validated on server in form actions. Errors automatically mapped to fields. No custom `getAndSetErrorsFromRes()` needed.
3. **Client-side validation** — same Zod schema runs client-side with `validators` option. Single source of truth.
4. **Cross-field validation** — Zod `.refine()` / `.superRefine()` with `path` targeting. Native, not a custom `postValidation` hook.
5. **Async validation** — server-side via form actions (natural for "is email taken?" checks). No debounced client-side API calls needed — the server action handles it.
6. **Nested data** — supports `form.data.address.street` paths. Error mapping via `setError(form, 'address.street', 'message')`.
7. **Tainted/dirty tracking** — built-in per-field dirty detection. No manual `touched` state.
8. **Snapshots** — form state persistence across page navigations. Free.
9. **Timers** — built-in debounce/throttle for submit. No custom `setTimeout` needed.

**What about the old form features?**

| Old Feature                    | Superforms Equivalent                                                    |
| ------------------------------ | ------------------------------------------------------------------------ |
| `PremadeInputs.email()`        | Zod schema + Formsnap `<Field>` with preset props (build a thin wrapper) |
| `DFormInput.onFilter`          | `oninput` handler on the `<input>` element (simple)                      |
| `customValidation` (async)     | Server-side in form action via `setError()`                              |
| `postValidation` (cross-field) | Zod `.refine()` / `.superRefine()` with `path`                           |
| `getAndSetErrorsFromRes()`     | Built-in — `$errors` store auto-populated from server                    |
| `validateForm()` polling       | `$validate()` — returns a Promise, no polling                            |
| Counter-based triggers         | Not needed — Superforms handles reactivity                               |
| `DFormContext` (SvelteMap)     | `superForm()` returns reactive stores                                    |

**What Superforms does NOT solve (we still build):**

- **PremadeInputs equivalent** — a thin layer of preset Zod schemas + component props for common fields (email, phone, address, etc.)
- **Address autocomplete** — domain-specific, wraps an external API (see §3.3)
- **Phone input with country picker** — domain-specific (see §3.4)
- **Dependent/cascading selects** — Superforms handles the data; the UI composition is on us

**Formsnap** adds accessible form markup (`<Field>`, `<Label>`, `<FieldErrors>`) with automatic ARIA attributes. It is what shadcn-svelte uses for its Form component. Using it means our form components get proper accessibility for free.

### 3.2 UI Components

#### Recommendation: **shadcn-svelte** (primary) + **Bits UI** (when shadcn lacks a component)

| Library                                    | Version     | Stars  | Approach                     | Svelte 5     |
| ------------------------------------------ | ----------- | ------ | ---------------------------- | ------------ |
| [shadcn-svelte](https://shadcn-svelte.com) | Latest 2026 | ~8,250 | Copy-paste styled (Tailwind) | Yes          |
| [Bits UI](https://bits-ui.com)             | 2.16.2      | ~3,100 | Headless primitives          | Yes (native) |
| [Melt UI](https://melt-ui.com)             | 0.86.6      | ~3,000 | Lower-level builders         | Yes          |
| [Skeleton UI](https://skeleton.dev)        | v5          | ~5,900 | Full design system (Zag.js)  | Yes          |

**Why shadcn-svelte:**

1. **You own the code** — components are copied into your project, not an npm dependency. Full control to customize.
2. **Built on Bits UI** — all the accessibility and keyboard navigation of headless primitives, with Tailwind styling on top.
3. **Tailwind v4 support** — uses the same CSS custom property theming pattern the old project already uses.
4. **Largest ecosystem** — most community components, recipes, and examples. The shadcn pattern is the dominant approach across React AND Svelte.
5. **Data table included** — wraps `@tanstack/table-core` with Svelte 5 snippets. Sorting, filtering, pagination, row selection.
6. **Form component** — wraps Superforms + Formsnap. Consistent form UI with minimal effort.
7. **Integrated toast** — uses svelte-sonner under the hood.

**Why NOT Skeleton UI:** Zag.js adds an abstraction layer, multi-framework focus dilutes Svelte attention, and the opinionated design system may fight with custom theming.

**Why NOT bare Bits UI only:** You'd have to style every component from scratch. shadcn-svelte gives you styled defaults you can customize.

**Key shadcn-svelte components relevant to D2-WORX:**

| Component                      | Old Equivalent                   | Notes                           |
| ------------------------------ | -------------------------------- | ------------------------------- |
| Button                         | `Button.svelte`                  | 12+ variants, loading state     |
| Dialog                         | `Modal.svelte`                   | Accessible, focus trap, portal  |
| Alert Dialog                   | Confirmation modals              | Destructive action confirmation |
| Combobox                       | `SelectInput.svelte` (search)    | Bits UI Combobox underneath     |
| Select                         | `SelectInput.svelte` (no search) | Keyboard accessible             |
| Popover                        | Ad-hoc in domain components      | Reusable floating content       |
| Tooltip                        | `Tooltip.svelte`                 | Accessible, configurable        |
| Tabs                           | None (inline)                    | Dashboard sections              |
| Accordion                      | None                             | FAQ, settings sections          |
| Form (Field/Label/FieldErrors) | Custom form components           | Formsnap + Superforms           |
| Data Table                     | `Table/*` (Svelte 4)             | TanStack Table core             |
| Calendar / DatePicker          | None                             | Bits UI date primitives         |
| Sidebar                        | `AccountNavDesktop.svelte`       | Collapsible, responsive         |
| Sonner (Toast)                 | `doToast()` (commented out)      | svelte-sonner                   |
| Card                           | Custom card CSS                  | Consistent card layouts         |
| Badge                          | None                             | Status indicators               |
| Separator                      | `Divider.svelte`                 | Semantic `<hr>`                 |
| Input / Textarea               | `TextInput.svelte`               | Base form elements              |
| Checkbox                       | `CheckboxInput.svelte`           | Accessible toggle               |
| Alert                          | `Alert.svelte`                   | Info/warning/error messages     |

### 3.3 Address Autocomplete

#### Recommendation: **Radar** (start) → Google Places (if global coverage needed)

| Provider                                                                                   | Free Tier     | Per-Request                 | Global Coverage          | Session Pricing       |
| ------------------------------------------------------------------------------------------ | ------------- | --------------------------- | ------------------------ | --------------------- |
| [Radar](https://radar.com/product/address-autocomplete-api)                                | 100,000/month | $0.50/1K                    | US focus, ~200 countries | No sessions needed    |
| [Google Places (new)](https://developers.google.com/maps/documentation/places/web-service) | 10,000/month  | $2.83/1K + $5-25/1K details | Full global              | Complex session model |
| [Mapbox](https://mapbox.com/address-autofill)                                              | Generous      | Per-session                 | NA/Europe focus          | Auto-managed          |
| [Loqate](https://loqate.com)                                                               | Credit-based  | Sales-driven                | 250 countries            | N/A                   |

**Why Radar first:**

- 100K free requests/month covers early-stage development and launch
- Simple REST API (`GET /search/autocomplete`) — no SDK bloat
- $0.50/1K is 82% cheaper than Google ($2.83/1K)
- US coverage is excellent (100% addresses) — fine for an SMB SaaS starting in the US
- No session management complexity

**Migration path to Google Places:** If international coverage gaps become a problem, switch to Google Places API. The autocomplete UI is the same (combobox with debounced search) — only the API endpoint and response mapping changes. Design the wrapper abstraction so the provider is swappable.

**Implementation approach:** Build a thin wrapper using Bits UI Combobox + a debounced fetch to the autocomplete API. On selection, fetch full address details and populate form fields via Superforms' `form.data` proxy. This replaces the old `AddressInput.svelte` entirely.

### 3.4 Phone Input

#### Recommendation: **intl-tel-input** (official Svelte 5 wrapper)

| Library                                                                  | Version | Svelte 5                               | Bundle                  | Notes                                |
| ------------------------------------------------------------------------ | ------- | -------------------------------------- | ----------------------- | ------------------------------------ |
| [intl-tel-input](https://www.npmjs.com/package/intl-tel-input)           | 26.7.5  | Yes (official `intl-tel-input/svelte`) | Moderate (flag sprites) | Most mature, actively maintained     |
| [libphonenumber-js](https://github.com/catamphetamine/libphonenumber-js) | 1.12.38 | N/A (validation only)                  | ~45-150KB               | Already in use, keep for server-side |
| [svelte-tel-input](https://www.npmjs.com/package/svelte-tel-input)       | 3.6.1   | Unclear                                | Small                   | Smaller community                    |

**Why intl-tel-input:**

- Official Svelte 5 component wrapper (`import IntlTelInput from 'intl-tel-input/svelte'`)
- Most mature phone input library in the JS ecosystem
- Flag sprites, country dropdown, validation, formatting built-in
- Uses libphonenumber under the hood

**Keep libphonenumber-js** for:

- Server-side validation in SvelteKit form actions
- Phone formatting in non-form contexts
- Already a dependency in the current web client

### 3.5 Toasts

#### Recommendation: **svelte-sonner**

| Library                                                      | Version | Svelte 5               | Notes                                 |
| ------------------------------------------------------------ | ------- | ---------------------- | ------------------------------------- |
| [svelte-sonner](https://github.com/wobsoriano/svelte-sonner) | 1.0.7   | Yes (runes + snippets) | Port of Emil Kowalski's Sonner        |
| svelte-french-toast                                          | N/A     | No (abandoned)         | Community forks exist but lack polish |

**Why svelte-sonner:**

- Beautiful default animations (stacking, swipe-to-dismiss)
- Promise toasts (loading → success/error)
- Custom snippet rendering for rich toast content
- Integrated into shadcn-svelte (consistent with the rest of the UI)
- Actively maintained, Svelte 5 native

This replaces the old `doToast()` utility which was completely commented out.

### 3.6 Data Tables

#### Recommendation: **shadcn-svelte data table** (wraps `@tanstack/table-core`)

| Library                  | Version | Svelte 5                  | Notes                                 |
| ------------------------ | ------- | ------------------------- | ------------------------------------- |
| shadcn-svelte data table | Latest  | Yes                       | Built on `@tanstack/table-core`       |
| `@tanstack/svelte-table` | 8.21.3  | **No** (v9 alpha pending) | Official adapter not ready            |
| svelte-headless-table    | 0.18.x  | **No** (abandoned)        | Maintainer confirmed no Svelte 5 port |

**Why shadcn-svelte's approach:**

- Uses `@tanstack/table-core` directly (framework-agnostic core), not the broken Svelte adapter
- Works with Svelte 5 snippets today
- Sorting, filtering, pagination, column visibility, row selection
- When TanStack Table v9 ships its Svelte 5 adapter, migration is straightforward (same core API)

### 3.7 Date/Time

#### Recommendation: **Bits UI date components** + **date-fns**

| Library                                     | Purpose           | Notes                                |
| ------------------------------------------- | ----------------- | ------------------------------------ |
| Bits UI Calendar/DatePicker/DateRangePicker | UI components     | Built on `@internationalized/date`   |
| [date-fns](https://date-fns.org/)           | Date manipulation | Tree-shakeable, ~18KB, functional    |
| Temporal API                                | Future (2027+)    | Chrome 144 shipped, Safari not ready |

shadcn-svelte wraps Bits UI's date components with styled versions including month/year dropdown selectors.

**Do NOT use Temporal API yet** — Safari support is still incomplete (March 2026). The polyfill is ~200KB. Revisit late 2026.

---

## 4. Auth Integration Strategy

### How BetterAuth Works with SvelteKit

BetterAuth integrates directly into SvelteKit as a server-side handler:

**Server-side (`hooks.server.ts`):**

```typescript
import { auth } from "$lib/server/auth";
import { svelteKitHandler } from "better-auth/svelte-kit";

export async function handle({ event, resolve }) {
  // Resolve session from cookie
  const session = await auth.api.getSession({
    headers: event.request.headers,
  });
  if (session) {
    event.locals.session = session.session;
    event.locals.user = session.user;
  }
  return svelteKitHandler({ event, resolve, auth });
}
```

**Client-side:**

```typescript
import { createAuthClient } from "better-auth/svelte";
export const authClient = createAuthClient({
  /* config */
});
```

### Proxy Pattern Details

The D2-WORX Auth service runs as a standalone Node.js + Hono server. SvelteKit proxies auth API calls to it. `svelteKitHandler` mounts BetterAuth routes at the SvelteKit server, creating the proxy automatically.

**What this means for the web client:**

1. **`event.locals.session`** is available in every `+layout.server.ts`, `+page.server.ts`, and `+server.ts` — server-side auth is free
2. **Route protection** should be in `hooks.server.ts` (server-side), not `+layout.ts` (client-side) — fixes the old project's flash-of-unauthenticated-content problem
3. **JWT for gateway calls** — obtained via `authClient.token()` on client, or via auth API on server. Stored in memory only (never localStorage)
4. **Org context** — JWT includes orgId, orgType, role. Client re-fetches JWT after org switch

### Auth Flow: Old vs. New

| Aspect           | DeCAF (old)                           | D2-WORX (new)                                       |
| ---------------- | ------------------------------------- | --------------------------------------------------- |
| Session check    | Client-only API call every navigation | Server-side in `hooks.server.ts` via `event.locals` |
| Route protection | `+layout.ts` redirect (client)        | `hooks.server.ts` redirect (server) — no flash      |
| Sign-in          | POST to monolith `/auth/sign-in`      | BetterAuth `authClient.signIn.email()` (proxied)    |
| Sign-up          | POST to monolith `/auth/sign-up`      | BetterAuth `authClient.signUp.email()` (proxied)    |
| API auth         | Cookie (`credentials: "include"`)     | JWT (`Authorization: Bearer`) to gateway            |
| Org context      | Session-level on monolith             | JWT claim + session field, refresh on switch        |
| Reactive session | Fetch on every navigation             | `authClient.useSession()` reactive store            |

### Impact on Forms

**Sign-up form:** BetterAuth handles user creation. The form submits via `authClient.signUp.email()`, NOT a SvelteKit form action. This means Superforms' progressive enhancement doesn't apply to auth forms (BetterAuth has its own API). For auth-specific forms, use Superforms for validation only (client-side Zod), with submission handled by BetterAuth client.

**Non-auth forms** (org creation, address entry, profile updates, etc.): Use Superforms with SvelteKit form actions normally. The form action on the server calls the .NET Gateway via gRPC/HTTP with a JWT.

### JWT Lifecycle on Client

1. User signs in via BetterAuth → session cookie set
2. Client calls `authClient.token()` → proxied to Auth service → returns JWT
3. JWT stored in memory (module-level variable, not localStorage)
4. JWT expires in 15 minutes → auto-refresh before expiry
5. On org switch → new JWT obtained with updated org claims
6. On sign-out → JWT discarded, session cookie cleared

---

## 5. Form System: Old vs. New

### Side-by-Side Comparison

| Feature                     | Old (Custom)                                         | New (Superforms + Formsnap)                              |
| --------------------------- | ---------------------------------------------------- | -------------------------------------------------------- |
| **Validation engine**       | Zod per-field, manual orchestration                  | Zod schema (single source), auto-orchestrated            |
| **Server validation**       | None (client-only)                                   | SvelteKit form actions with `setError()`                 |
| **Progressive enhancement** | No (JS required)                                     | Yes (`use:enhance`, works without JS)                    |
| **Cross-field validation**  | `postValidation` callback                            | Zod `.refine()` / `.superRefine()`                       |
| **Async validation**        | `customValidation` callback (client-side, debounced) | Server-side in form action (natural for DB checks)       |
| **Input filtering**         | `onFilter` callback                                  | `oninput` handler on element (simpler)                   |
| **Error display**           | Custom `FormErrors.svelte`                           | Formsnap `<FieldErrors>` (accessible, ARIA)              |
| **Field state**             | `DFormInput` class + local `$state` copies           | Superforms proxy stores (`$form`, `$errors`, `$tainted`) |
| **Form submission**         | `validateForm()` polling + manual fetch              | `use:enhance` or `$submit()` — handles everything        |
| **Loading state**           | Manual `loading` boolean per field + global          | Built-in `$submitting`, `$delayed`, `$timeout`           |
| **Preset fields**           | `PremadeInputs` static factory                       | Zod schema presets + component prop presets              |
| **Dependent selects**       | `dependentOptions` + async callback                  | Reactive `$derived` from form data + server load         |
| **Address autocomplete**    | `AddressInput.svelte` with API integration           | Bits UI Combobox + API wrapper (cleaner separation)      |
| **Phone input**             | Custom `PhoneInput.svelte` with libphonenumber       | `intl-tel-input/svelte` (official, maintained)           |
| **Accessibility**           | Manual (inconsistent)                                | Formsnap ARIA attributes (automatic)                     |
| **Code duplication**        | ~50 lines repeated in every component                | Zero (Superforms handles state)                          |

### Handling Complex Interdependencies

The user specifically called out form interdependencies. Here's how each scenario maps:

**Scenario 1: Country → Region cascading select**

Old approach: `SelectInput` with `dependentOptions` prop and `setDependentOptionsBasedOnTextValueFunction` callback.

New approach with Superforms:

```svelte
<!-- Country changes → region options update reactively -->
<script>
  const { form } = superForm(data.form);
  // Regions filter reactively based on selected country
  let regions = $derived(allRegions.filter((r) => r.countryCode === $form.country));
</script>

<Form.Field name="country">
  <Combobox
    items={countries}
    bind:value={$form.country}
    onValueChange={() => {
      $form.region = "";
    }}
  />
</Form.Field>

<Form.Field name="region">
  <Combobox
    items={regions}
    bind:value={$form.region}
  />
</Form.Field>
```

The `$derived` reactive binding handles the cascading naturally. When country changes, regions recompute. The `onValueChange` callback clears the region field. No custom `dependentOptions` mechanism needed.

**Scenario 2: Address autocomplete populating multiple fields**

Old approach: `AddressInput.svelte` with `setAddressDetails` callback that mutates multiple DFormInput values through the SvelteMap.

New approach:

```svelte
<script>
  const { form } = superForm(data.form);

  async function onAddressSelect(placeId: string) {
    const address = await fetchAddressDetails(placeId);
    // Superforms proxy lets you set multiple fields at once
    $form.street = address.street;
    $form.city = address.city;
    $form.region = address.region;
    $form.postalCode = address.postalCode;
    $form.country = address.country;
  }
</script>

<AddressCombobox
  onSelect={onAddressSelect}
  bind:searchValue={$form.street}
/>
```

Superforms' proxy store (`$form`) is directly mutable. Setting `$form.city = "..."` updates the form state, triggers validation, and updates the UI. No SvelteMap, no `updateField()`, no `doNotReSearch` flag.

**Scenario 3: Password match cross-field validation**

Old approach: `passwordMatchPostValidation` as a `postValidation` callback that reads both fields from the SvelteMap.

New approach — single Zod schema:

```typescript
const signUpSchema = z
  .object({
    password: z.string().min(8).max(64),
    confirmPassword: z.string(),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Passwords must match",
    path: ["confirmPassword"],
  });
```

The `.refine()` runs automatically during validation. The `path` option maps the error to the `confirmPassword` field. No custom callback, no form context inspection.

### What We Still Build Custom

Even with Superforms + shadcn-svelte, some things are domain-specific:

1. **Field presets** — a `fields.ts` file with pre-configured Zod schemas and component props for common fields (email, phone, address lines, postal code, etc.). Replaces `PremadeInputs`.

2. **Address autocomplete wrapper** — Bits UI Combobox + Radar/Google API. This is a reusable component but not something any library provides.

3. **Org-aware form actions** — server-side form actions that resolve JWT, call gateway, map errors. A thin utility layer.

4. **Multi-step forms** — checkout-style wizards. Superforms supports multi-step natively, but the UI composition (step indicator, navigation, state persistence) is on us.

---

## 6. Proposed Architecture

### Route Structure

```
src/routes/
├── (auth)/                          # Auth pages (public, BetterAuth forms)
│   ├── sign-in/+page.svelte
│   ├── sign-up/+page.svelte
│   ├── forgot-password/+page.svelte
│   ├── reset-password/+page.svelte
│   └── verify-email/+page.svelte
├── (onboarding)/                    # Post-auth, pre-org pages
│   ├── +layout.svelte               # Requires auth, no org needed
│   ├── welcome/+page.svelte         # Landing for users with no orgs
│   ├── create-org/+page.svelte
│   └── select-org/+page.svelte
├── (app)/                           # Main authenticated app
│   ├── +layout.svelte               # Requires auth + active org
│   ├── (customer)/                  # Customer org type routes
│   │   ├── dashboard/+page.svelte
│   │   ├── jobs/+page.svelte
│   │   ├── invoices/+page.svelte
│   │   └── ...
│   ├── (support)/                   # Support org type routes
│   │   └── ...
│   ├── (admin)/                     # Admin org type routes
│   │   └── ...
│   └── (shared)/                    # Routes shared across org types
│       ├── settings/+page.svelte
│       └── profile/+page.svelte
├── (public)/                        # Marketing pages (no auth)
│   ├── +layout.svelte
│   ├── +page.svelte                 # Homepage
│   ├── pricing/+page.svelte
│   └── about/+page.svelte
├── api/                             # API routes
│   └── auth/[...path]/+server.ts   # BetterAuth proxy catch-all
├── +layout.svelte                   # Root: CSS, favicon, OTel
├── +layout.server.ts                # Root: session resolution
├── +error.svelte                    # Global error page
└── +page.svelte                     # Redirect to (public) or (app)
```

### Component Organization

```
src/lib/
├── components/
│   ├── ui/                          # shadcn-svelte components (copied)
│   │   ├── button/
│   │   ├── dialog/
│   │   ├── combobox/
│   │   ├── form/
│   │   ├── data-table/
│   │   ├── sonner/
│   │   └── ...
│   ├── forms/                       # Domain form components
│   │   ├── fields.ts                # Zod schema presets (replaces PremadeInputs)
│   │   ├── address-combobox.svelte  # Address autocomplete wrapper
│   │   ├── phone-input.svelte       # intl-tel-input wrapper
│   │   └── country-select.svelte    # Country combobox with flags
│   ├── layout/                      # Layout components
│   │   ├── app-sidebar.svelte
│   │   ├── nav-header.svelte
│   │   └── public-nav.svelte
│   └── domain/                      # Domain-specific composites
│       ├── org-creation-form.svelte
│       ├── payment-method-card.svelte
│       └── ...
├── server/                          # Server-only code
│   ├── auth.ts                      # BetterAuth server config
│   ├── gateway.ts                   # Gateway HTTP client (JWT-authenticated)
│   ├── logger.server.ts             # Structured logger (exists)
│   └── request-logger.server.ts     # Request logger (exists)
├── stores/                          # Client-side reactive state
│   ├── auth.ts                      # authClient + useSession
│   └── jwt.ts                       # JWT lifecycle (obtain, refresh, memory-only)
├── utils/
│   ├── cn.ts                        # clsx + tailwind-merge
│   ├── api.ts                       # Client-side gateway client (JWT from memory)
│   └── idempotency.ts               # Idempotency-Key generation
├── paraglide/                       # Generated i18n (exists)
└── assets/                          # Static assets (exists)
```

### Technology Stack Summary

| Concern              | Library                                     | Replaces                              |
| -------------------- | ------------------------------------------- | ------------------------------------- |
| UI primitives        | shadcn-svelte (Bits UI)                     | Custom Button, Modal, Tooltip, Alert  |
| Forms                | Superforms + Formsnap                       | Custom DFormInput/DFormContext system |
| Validation           | Zod (already in use)                        | Same (Zod)                            |
| Toast                | svelte-sonner                               | `doToast()` (was broken)              |
| Data tables          | shadcn-svelte data table                    | Svelte 4 Table components             |
| Date/time            | Bits UI date components + date-fns          | None (new)                            |
| Phone input          | intl-tel-input (Svelte 5 wrapper)           | Custom PhoneInput.svelte              |
| Address autocomplete | Bits UI Combobox + Radar API                | Custom AddressInput.svelte            |
| Auth client          | better-auth/svelte                          | Custom DeCAFBE auth methods           |
| API client           | Custom (two paths: server + client)         | DeCAFBE singleton                     |
| i18n                 | Paraglide (already set up)                  | None (was missing)                    |
| Icons                | TBD (Lucide via shadcn-svelte, or FA)       | FontAwesome Kit CDN                   |
| Styling              | Tailwind v4 + CSS custom properties         | Same approach (carry forward)         |
| Server observability | OTel (already wired)                        | None (new)                            |
| Client observability | Grafana Faro (errors + traces + Web Vitals) | None (new)                            |
| Component testing    | vitest-browser-svelte (already configured)  | None                                  |
| E2E testing          | Playwright (already configured)             | None                                  |
| A11y testing         | axe-core/playwright + Svelte compiler       | None                                  |
| Visual regression    | Playwright screenshots → Argos CI           | None                                  |
| Performance          | Lighthouse CI + Web Vitals                  | None                                  |
| Animations           | tailwindcss-motion + Svelte transitions     | Same                                  |

### Backend Package Reuse (SvelteKit Server-Side)

These `@d2/*` packages can be used in SvelteKit's server-side code (`+page.server.ts`, `+server.ts`, `hooks.server.ts`):

| Package            | Use Case                                          |
| ------------------ | ------------------------------------------------- |
| `@d2/result`       | Consistent result handling from gateway responses |
| `@d2/utilities`    | String/array/UUID helpers                         |
| `@d2/logging`      | Structured logging (if replacing custom logger)   |
| `@d2/geo-client`   | WhoIs lookup for request enrichment (if needed)   |
| `@d2/comms-client` | Publishing notifications from SvelteKit server    |

Note: Packages requiring Redis, RabbitMQ, or gRPC connections (`@d2/cache-redis`, `@d2/messaging`) should NOT be used directly from SvelteKit — go through the .NET Gateway instead.

---

## 7. Frontend Testing Strategy

The old DeCAF project had **zero tests**. No component tests, no E2E tests, no accessibility tests. This section establishes a comprehensive testing strategy.

### 7.1 Component Testing

#### Recommendation: **vitest-browser-svelte** (Vitest 4+ browser mode)

| Tool                                                                         | Version | Notes                             |
| ---------------------------------------------------------------------------- | ------- | --------------------------------- |
| [vitest-browser-svelte](https://github.com/vitest-dev/vitest-browser-svelte) | 2.0.2   | Official Vitest community package |
| `@vitest/browser`                                                            | 4.0.18  | Already installed                 |
| `@vitest/browser-playwright`                                                 | 4.0.18  | Already installed                 |

The current SvelteKit project already has Vitest browser mode configured with Playwright. This is the **officially recommended** approach from Svelte's documentation.

**Why browser mode over jsdom:**

- Runs tests in a real Chromium browser via Playwright — no missing browser APIs
- Svelte 5 runes (`$state`, `$derived`, `$effect`) work correctly (jsdom has known issues with runes reactivity)
- CSS layout, `ResizeObserver`, `IntersectionObserver`, real events all work
- No `act()` wrapper needed — locators have built-in retry-ability
- `expect.element()` auto-retries assertions (no `flushSync`/`tick` needed for DOM checks)

**Example — testing a component with runes:**

```typescript
// Button.svelte.test.ts
import { page } from "@vitest/browser/context";
import { render } from "vitest-browser-svelte";
import Button from "./Button.svelte";

it("shows loading state when clicked", async () => {
  render(Button, {
    label: "Submit",
    onClick: async () => {
      await delay(100);
    },
  });
  const button = page.getByRole("button", { name: "Submit" });
  await button.click();
  await expect.element(button).toHaveAttribute("aria-busy", "true");
});
```

**What NOT to use:**

- `@testing-library/svelte` — works with Svelte 5 but runs in jsdom. Known issues with runes (#284). Considered the legacy approach.

**What to test at this level:**

- Component renders correctly with different props
- User interactions (click, type, focus) trigger expected behavior
- Reactive state updates reflected in DOM
- Conditional rendering (loading, error, empty states)
- Accessibility attributes (ARIA roles, labels)
- Slot/snippet content rendering

### 7.2 E2E Testing with Playwright

Already configured (`playwright.config.ts` + `e2e/` directory). Playwright is the standard for SvelteKit E2E testing.

#### Auth Flow Testing

Playwright's [Authentication](https://playwright.dev/docs/auth) pattern saves significant time — authenticate once, reuse state:

```typescript
// playwright.config.ts
export default defineConfig({
  projects: [
    { name: "setup", testMatch: /.*\.setup\.ts/ },
    {
      name: "chromium",
      use: {
        ...devices["Desktop Chrome"],
        storageState: "playwright/.auth/user.json",
      },
      dependencies: ["setup"],
    },
  ],
});
```

```typescript
// e2e/auth.setup.ts
import { test as setup } from "@playwright/test";

setup("authenticate", async ({ page }) => {
  await page.goto("/sign-in");
  await page.getByLabel("Email").fill("test@example.com");
  await page.getByLabel("Password").fill("password123");
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.waitForURL("/dashboard");
  await page.context().storageState({ path: "playwright/.auth/user.json" });
});
```

Since BetterAuth uses cookie-based sessions, `storageState` captures session cookies. All subsequent tests reuse the authenticated state — ~60-80% faster than authenticating per test.

#### Form Testing

```typescript
test("shows validation errors on empty submit", async ({ page }) => {
  await page.goto("/create-org");
  await page.getByRole("button", { name: "Create" }).click();
  await expect(page.getByText("Organization name is required")).toBeVisible();
});

test("handles server error gracefully", async ({ page }) => {
  await page.route("**/create-org", (route) =>
    route.fulfill({ status: 500, body: "Internal Server Error" }),
  );
  await page.goto("/create-org");
  await page.getByLabel("Name").fill("Acme Corp");
  await page.getByRole("button", { name: "Create" }).click();
  await expect(page.getByText("Something went wrong")).toBeVisible();
});
```

#### Combobox / Autocomplete Testing

```typescript
test("address autocomplete populates fields", async ({ page }) => {
  await page.goto("/create-org");
  const input = page.getByRole("combobox", { name: "Street Address" });
  await input.fill("123 Main");
  await expect(page.getByRole("listbox")).toBeVisible();
  await page.getByRole("option", { name: /123 Main St/ }).click();
  // Verify dependent fields were populated
  await expect(page.getByLabel("City")).not.toHaveValue("");
  await expect(page.getByLabel("State")).not.toHaveValue("");
});
```

#### Responsive Testing

```typescript
// playwright.config.ts
import { devices } from "@playwright/test";

export default defineConfig({
  projects: [
    { name: "Desktop Chrome", use: { ...devices["Desktop Chrome"] } },
    { name: "Mobile Safari", use: { ...devices["iPhone 14"] } },
    { name: "Tablet", use: { ...devices["iPad Pro 11"] } },
  ],
});
```

#### Multi-Step Wizard Testing

```typescript
test("complete onboarding wizard", async ({ page }) => {
  await test.step("Step 1: Organization info", async () => {
    await page.goto("/create-org");
    await page.getByLabel("Name").fill("Acme Corp");
    await page.getByRole("button", { name: "Next" }).click();
  });

  await test.step("Step 2: Address", async () => {
    await page.getByLabel("Street").fill("123 Main St");
    await page.getByRole("button", { name: "Next" }).click();
  });

  await test.step("Step 3: Confirmation", async () => {
    await expect(page.getByText("Acme Corp")).toBeVisible();
    await page.getByRole("button", { name: "Create" }).click();
    await page.waitForURL("/dashboard");
  });
});
```

### 7.3 Accessibility Testing

Three layers, from fastest to most thorough:

#### Layer 1: Svelte Compiler Warnings (build-time, free)

Svelte is an "a11y-first framework" with **built-in compiler warnings** for:

- Missing `alt` text on images
- Missing keyboard handlers alongside click events
- Incorrect ARIA attributes
- Non-interactive elements with event handlers

These fire at compile time. `svelte-check` surfaces them in CI. Already active via the project's ESLint + svelte-check config.

**Limitation:** Only catches attribute-level issues on single elements. Cannot detect heading hierarchy, focus management, color contrast, or dynamic ARIA state issues.

#### Layer 2: axe-core in Playwright E2E (runtime, automated)

```bash
pnpm install -D @axe-core/playwright
```

```typescript
import AxeBuilder from "@axe-core/playwright";

test("dashboard has no a11y violations", async ({ page }) => {
  await page.goto("/dashboard");
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
    .analyze();
  expect(results.violations).toEqual([]);
});

// Scan a specific region
test("navigation is accessible", async ({ page }) => {
  await page.goto("/");
  const results = await new AxeBuilder({ page }).include("nav").analyze();
  expect(results.violations).toEqual([]);
});
```

axe-core catches ~57% of WCAG issues automatically. Add an axe scan to a dedicated a11y test suite that visits all major pages.

#### Layer 3: Manual Testing (periodic)

No automated tool catches everything. Periodic manual testing with:

- Screen reader (NVDA on Windows, VoiceOver on macOS)
- Keyboard-only navigation
- High contrast mode
- Zoom to 200%

### 7.4 Visual Regression Testing

#### Recommendation: Start with **Playwright built-in screenshots**, upgrade to **Argos CI** if needed

| Approach                             | Cost             | Setup  | Review UX         | Recommendation          |
| ------------------------------------ | ---------------- | ------ | ----------------- | ----------------------- |
| Playwright screenshots               | Free             | Low    | Git diffs only    | **Start here**          |
| [Argos CI](https://argos-ci.com)     | Free (OSS)       | Medium | Excellent web UI  | Best value upgrade      |
| [Lost Pixel](https://lost-pixel.com) | Free (self-host) | Medium | Good              | OSS alternative         |
| [Chromatic](https://chromatic.com)   | Paid             | Low    | Excellent         | Only if using Storybook |
| [Percy](https://percy.io)            | Paid ($400+/mo)  | Low    | AI-powered review | Enterprise scale        |

**Playwright built-in approach:**

```typescript
test("homepage visual regression", async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveScreenshot("homepage.png", {
    maxDiffPixelRatio: 0.01,
  });
});

// Element-level screenshot
test("sidebar visual regression", async ({ page }) => {
  const sidebar = page.getByRole("navigation");
  await expect(sidebar).toHaveScreenshot("sidebar.png");
});
```

Baselines committed to git. Run `--update-snapshots` to regenerate. Free, works offline.

**When to upgrade:** If manual baseline management becomes painful or you want a proper review UI for screenshot diffs, add Argos CI (used by MUI/Material UI).

### 7.5 Performance Testing

#### Lighthouse CI in GitHub Actions

```bash
pnpm install -D @lhci/cli
```

```jsonc
// lighthouserc.json
{
  "ci": {
    "collect": {
      "startServerCommand": "pnpm preview",
      "url": ["http://localhost:4173/", "http://localhost:4173/sign-in"],
    },
    "assert": {
      "assertions": {
        "categories:performance": ["error", { "minScore": 0.9 }],
        "categories:accessibility": ["error", { "minScore": 0.95 }],
        "categories:best-practices": ["warn", { "minScore": 0.9 }],
      },
    },
  },
}
```

This runs on every PR and fails the build if performance or accessibility scores drop below thresholds.

#### Web Vitals (Real User Monitoring)

See [§8.3](#83-web-vitals-rum) — Web Vitals collection is part of the client-side telemetry strategy.

### 7.6 Test Organization

```
clients/web/
├── src/
│   ├── lib/
│   │   ├── components/
│   │   │   ├── ui/
│   │   │   │   ├── button/
│   │   │   │   │   ├── button.svelte
│   │   │   │   │   └── button.svelte.test.ts    # Component test (browser mode)
│   │   │   │   └── ...
│   │   │   └── forms/
│   │   │       ├── address-combobox.svelte
│   │   │       └── address-combobox.svelte.test.ts
│   │   ├── server/
│   │   │   ├── gateway.ts
│   │   │   └── gateway.test.ts                  # Server util test (Node mode)
│   │   └── utils/
│   │       ├── format.ts
│   │       └── format.test.ts                   # Pure function test (Node mode)
├── e2e/
│   ├── auth.setup.ts                            # Playwright auth fixture
│   ├── auth.spec.ts                             # Auth flow E2E tests
│   ├── onboarding.spec.ts                       # Onboarding wizard E2E
│   ├── dashboard.spec.ts                        # Dashboard E2E
│   ├── a11y.spec.ts                             # Dedicated a11y scan suite
│   └── visual.spec.ts                           # Visual regression screenshots
├── playwright.config.ts
└── vite.config.ts                               # Vitest multi-project config
```

**File naming conventions:**

| Pattern            | Runner     | Environment          | What It Tests                             |
| ------------------ | ---------- | -------------------- | ----------------------------------------- |
| `*.svelte.test.ts` | Vitest     | Browser (Playwright) | Svelte components                         |
| `*.test.ts`        | Vitest     | Node                 | Server utils, pure functions, Zod schemas |
| `*.spec.ts`        | Playwright | Browser (full app)   | E2E user flows                            |

**What to test at each level:**

| Level       | Tool                   | Tests                                               | Speed  |
| ----------- | ---------------------- | --------------------------------------------------- | ------ |
| Unit        | Vitest (Node)          | Pure functions, Zod schemas, utils, server helpers  | ~ms    |
| Component   | vitest-browser-svelte  | Rendering, interactions, runes reactivity, ARIA     | ~100ms |
| E2E         | Playwright             | Full user flows, auth, forms→server, multi-page nav | ~1-5s  |
| A11y        | axe-core + Playwright  | WCAG violations on all major pages                  | ~2s    |
| Visual      | Playwright screenshots | Key pages and component states                      | ~2-5s  |
| Performance | Lighthouse CI          | Core Web Vitals, performance budgets                | ~30s   |

### Storybook: Not Now

Storybook has Svelte 5 support (`@storybook/sveltekit` + `@storybook/addon-svelte-csf` v5), but adds significant build complexity. Defer adoption until the component library is large enough to benefit from isolated development and a visual catalog. `vitest-browser-svelte` covers component testing needs without the overhead.

---

## 8. Client-Side Telemetry & Error Monitoring

The old DeCAF project had **zero client-side observability**. If a user saw an error, the only way to know was if they reported it. This section establishes a strategy to catch errors on the client, track real user experience, and correlate browser activity with backend traces.

### 8.1 Error Capture Strategy

SvelteKit has multiple error boundaries. A comprehensive strategy uses ALL of them:

| Error Source                  | Capture Mechanism                               | Current State                             |
| ----------------------------- | ----------------------------------------------- | ----------------------------------------- |
| Rendering errors              | `<svelte:boundary onerror={...}>`               | **Missing**                               |
| Client load function errors   | `hooks.client.ts` → `handleError`               | **Missing** (no `hooks.client.ts`)        |
| Server-side unexpected errors | `hooks.server.ts` → `handleError`               | **Missing** (exists but no `handleError`) |
| Unhandled JS exceptions       | `window.onerror`                                | **Missing**                               |
| Unhandled promise rejections  | `window.addEventListener('unhandledrejection')` | **Missing**                               |
| Network/fetch errors          | OTel fetch instrumentation or Faro              | **Missing**                               |
| 4xx/5xx API responses         | Custom fetch wrapper                            | **Missing**                               |
| Error display to user         | `+error.svelte` pages                           | **Missing**                               |

#### Files Needed

**`src/hooks.client.ts`** — catches unexpected errors during client-side navigation and load functions:

```typescript
import type { HandleClientError } from "@sveltejs/kit";

export const handleError: HandleClientError = async ({ error, event, status, message }) => {
  const errorId = crypto.randomUUID();

  // Send to telemetry (Faro, custom endpoint, or OTel)
  await fetch("/api/client-error", {
    method: "POST",
    body: JSON.stringify({
      errorId,
      message: error instanceof Error ? error.message : String(error),
      stack: error instanceof Error ? error.stack : undefined,
      url: event.url.pathname,
      status,
    }),
    headers: { "Content-Type": "application/json" },
  }).catch(() => {}); // Don't let reporting fail the error flow

  return { message: "An unexpected error occurred", code: errorId };
};
```

**`src/hooks.server.ts`** — add `handleError` export to existing file:

```typescript
import type { HandleServerError } from "@sveltejs/kit";

export const handleError: HandleServerError = async ({ error, event, status, message }) => {
  const errorId = crypto.randomUUID();
  logger.error("Unexpected server error", {
    errorId,
    error: error instanceof Error ? error.message : String(error),
    stack: error instanceof Error ? error.stack : undefined,
    url: event.url.pathname,
    status,
  });
  return { message: "An unexpected error occurred", code: errorId };
};
```

**`src/app.d.ts`** — define the error shape:

```typescript
declare global {
  namespace App {
    interface Error {
      message: string;
      code: string; // errorId for support reference
    }
  }
}
```

**`src/routes/+error.svelte`** — user-facing error page:

```svelte
<script>
  import { page } from "$app/state";
</script>

<div>
  <h1>{$page.status}</h1>
  <p>{$page.error?.message}</p>
  {#if $page.error?.code}
    <p>Reference: {$page.error.code}</p>
  {/if}
</div>
```

**`src/error.html`** — catastrophic fallback (SvelteKit itself failed to load):

```html
<!DOCTYPE html>
<html>
  <body>
    <h1>%sveltekit.status%</h1>
    <p>%sveltekit.error.message%</p>
  </body>
</html>
```

#### `<svelte:boundary>` for Component Errors

Svelte 5's error boundary catches rendering errors at the component level:

```svelte
<svelte:boundary onerror={(error, reset) => reportError(error)}>
  <FlakyComponent />
  {#snippet failed(error, reset)}
    <p>Something went wrong. <button onclick={reset}>Retry</button></p>
  {/snippet}
</svelte:boundary>
```

**Limitation:** Only catches errors during rendering. Does NOT catch errors in event handlers, `setTimeout`, or async work. Those still need `window.onerror` / `unhandledrejection` listeners.

### 8.2 Browser Observability (Grafana Faro)

#### Recommendation: **Grafana Faro** as the primary client-side telemetry SDK

The project already runs the full LGTM stack (Loki, Grafana, Tempo, Mimir) with Alloy as the collector. Grafana Faro is the natural client-side complement — it sends data directly into the existing stack with zero new infrastructure.

| Feature                | Grafana Faro           | Sentry                    | Raw OTel Browser SDK |
| ---------------------- | ---------------------- | ------------------------- | -------------------- |
| Error tracking         | Yes → Loki             | Yes → Sentry              | Via span events      |
| Browser traces         | Yes → Tempo            | Yes → Sentry              | Yes → Tempo          |
| Web Vitals             | Yes (built-in) → Mimir | Yes (auto)                | Manual wiring        |
| Console capture        | Yes (auto) → Loki      | Via breadcrumbs           | No                   |
| Session replay         | No                     | Yes (36 KB extra)         | No                   |
| LGTM integration       | **Native**             | Separate system           | Native (manual)      |
| Bundle size (gzip)     | ~15-30 KB              | ~41 KB (+36 KB replay)    | ~40-60 KB            |
| Official SvelteKit SDK | No (manual init)       | Yes (`@sentry/sveltekit`) | No                   |
| License                | Apache 2.0             | BSL (paid features)       | Apache 2.0           |

**Why Faro over raw OTel browser SDK:**

- Faro wraps OTel internally but adds higher-level APIs (error capture, console interception, Web Vitals)
- Alloy already has a `faro.receiver` component with built-in CORS support — the raw OTLP receiver does NOT handle CORS
- Simpler initialization — one `initializeFaro()` call vs manually wiring TracerProvider + MeterProvider + LoggerProvider
- Same destination (Tempo, Loki, Mimir) — no new infrastructure

**Why Faro over Sentry:**

- No new vendor or separate dashboard — errors appear in Grafana alongside backend traces
- Distributed tracing works end-to-end: browser click → Faro span → Alloy → Tempo → correlates with backend spans
- Free, open source, self-hosted
- The main trade-off: no session replay (see §8.4)

#### Alloy Configuration

Add `faro.receiver` to existing Alloy config:

```alloy
faro.receiver "default" {
    server {
        listen_address = "0.0.0.0"
        listen_port = 12347
        cors_allowed_origins = ["http://localhost:5173", "https://your-domain.com"]
    }

    output {
        logs   = [loki.write.default.receiver]
        traces = [otelcol.exporter.otlp.tempo.input]
    }
}
```

#### SvelteKit Client Initialization

```typescript
// src/lib/telemetry/faro.client.ts
import { initializeFaro, getWebInstrumentations } from "@grafana/faro-web-sdk";
import { TracingInstrumentation } from "@grafana/faro-web-tracing";

export function initFaro() {
  if (typeof window === "undefined") return; // SSR guard

  return initializeFaro({
    url: "http://localhost:12347/collect", // Alloy faro.receiver
    app: { name: "d2-sveltekit", version: "0.0.1" },
    instrumentations: [
      ...getWebInstrumentations({
        captureConsole: true, // console.error → Loki
        captureConsoleDisabledLevels: ["debug", "trace", "log"],
      }),
      new TracingInstrumentation(), // fetch spans → Tempo
    ],
  });
}
```

```svelte
<!-- src/routes/+layout.svelte -->
<script>
  import { browser } from "$app/environment";
  import { onMount } from "svelte";

  onMount(() => {
    if (browser) {
      import("$lib/telemetry/faro.client").then(({ initFaro }) => initFaro());
    }
  });
</script>
```

Lazy-loading Faro in `onMount` ensures it doesn't block initial page render or inflate SSR bundles.

#### What Faro Captures Automatically

| Signal           | Destination | What It Includes                                                        |
| ---------------- | ----------- | ----------------------------------------------------------------------- |
| Errors           | Loki        | Unhandled exceptions, promise rejections, stack traces                  |
| Console          | Loki        | `console.error`, `console.warn`, `console.info` (configurable)          |
| Browser traces   | Tempo       | `fetch()` calls with W3C Traceparent headers (correlates with backend!) |
| Page loads       | Tempo       | Document load timing (DNS, TCP, TTFB, DOM, load event)                  |
| Web Vitals       | Mimir       | LCP, FID/INP, CLS, TTFB, FCP — automatic                                |
| Session metadata | All         | Session ID, user ID (if set), page URL, browser/OS                      |

The key win: a user's click in the browser that triggers a `fetch()` to the .NET gateway gets a `traceparent` header injected by Faro. The gateway propagates this to gRPC calls to backend services. In Grafana, you see the **entire trace** — browser → gateway → Geo/Auth/Comms — in a single view.

### 8.3 Web Vitals (RUM)

Faro collects Web Vitals automatically. If you want more control or want to report metrics without Faro, use the `web-vitals` library directly:

```bash
pnpm install web-vitals  # ~1.5 KB gzipped
```

```typescript
import { onLCP, onINP, onCLS, onTTFB, onFCP } from "web-vitals";

// Report to custom endpoint
function sendMetric(metric) {
  fetch("/api/web-vitals", {
    method: "POST",
    body: JSON.stringify({
      name: metric.name,
      value: metric.value,
      rating: metric.rating, // "good", "needs-improvement", "poor"
      id: metric.id,
      url: location.pathname,
    }),
  }).catch(() => {});
}

onLCP(sendMetric);
onINP(sendMetric);
onCLS(sendMetric);
onTTFB(sendMetric);
onFCP(sendMetric);
```

**Current Core Web Vitals thresholds (2025-2026):**

| Metric                          | Good    | Needs Improvement | Poor    |
| ------------------------------- | ------- | ----------------- | ------- |
| LCP (Largest Contentful Paint)  | < 2.5s  | 2.5-4s            | > 4s    |
| INP (Interaction to Next Paint) | < 200ms | 200-500ms         | > 500ms |
| CLS (Cumulative Layout Shift)   | < 0.1   | 0.1-0.25          | > 0.25  |
| TTFB (Time to First Byte)       | < 800ms | 800ms-1.8s        | > 1.8s  |
| FCP (First Contentful Paint)    | < 1.8s  | 1.8-3s            | > 3s    |

**SvelteKit-specific notes:**

- SSR pages: TTFB and FCP reflect server rendering + hydration time
- After initial load, SvelteKit navigations are client-side. LCP/FCP won't fire for these. INP is the main metric for SPA interactivity
- Svelte 5 delivers ~50% smaller bundles than Svelte 4, which directly improves LCP

### 8.4 Session Replay

Faro does NOT include session replay. If you need to visually see what a user experienced:

| Tool                                                            | Bundle (gzip) | Privacy             | Self-Host          | Notes                            |
| --------------------------------------------------------------- | ------------- | ------------------- | ------------------ | -------------------------------- |
| [Sentry Replay](https://docs.sentry.io/product/session-replay/) | ~36 KB (lazy) | Default mask-all    | Yes (complex)      | Most mature, `@sentry/sveltekit` |
| [PostHog](https://posthog.com/session-replay)                   | ~52 KB core   | rrweb-based masking | Yes (MIT, Docker)  | Full product analytics suite     |
| [rrweb](https://github.com/rrweb-io/rrweb)                      | ~19-29 KB     | Annotation-based    | You build pipeline | Library only, maximum control    |

**Privacy considerations (all tools):**

- Default to "mask all text" — text replaced with asterisks before leaving browser
- CSS class annotations: `.rr-mask` (blur), `.rr-ignore` (exclude), `.rr-block` (remove)
- Masking happens client-side — sensitive data never reaches the server
- GDPR: Requires explicit consent (cookie banner), privacy policy disclosure

**Recommendation:** Don't add session replay at launch. Start with Faro (errors + traces + Web Vitals). If specific UX bugs are hard to reproduce from logs alone, add Sentry Replay as a targeted addition — it can coexist with Faro since they serve different purposes (Sentry for replay, Faro for LGTM integration).

### 8.5 What's Missing Today

Current state of the SvelteKit web client's observability:

| Concern             | Server-Side                           | Client-Side                                 |
| ------------------- | ------------------------------------- | ------------------------------------------- |
| Error capture       | Partial (logging, no `handleError`)   | **Nothing**                                 |
| Structured logging  | Yes (OTel logger)                     | **Nothing**                                 |
| Request tracing     | Yes (OTel SDK + auto-instrumentation) | **Nothing** (packages installed, not wired) |
| Web Vitals          | N/A                                   | **Nothing**                                 |
| Error page          | N/A                                   | **Nothing** (no `+error.svelte`)            |
| Fallback error page | N/A                                   | **Nothing** (no `src/error.html`)           |

**Files to create/modify to reach baseline:**

| File                                      | Action                             | Priority |
| ----------------------------------------- | ---------------------------------- | -------- |
| `src/hooks.client.ts`                     | Create — `handleError` + Faro init | High     |
| `src/hooks.server.ts`                     | Add `handleError` export           | High     |
| `src/app.d.ts`                            | Define `App.Error` interface       | High     |
| `src/routes/+error.svelte`                | Create — user-facing error page    | High     |
| `src/error.html`                          | Create — catastrophic fallback     | High     |
| `src/lib/telemetry/faro.client.ts`        | Create — Faro initialization       | Medium   |
| `src/routes/+layout.svelte`               | Add lazy Faro import in `onMount`  | Medium   |
| `observability/alloy/config/config.alloy` | Add `faro.receiver` block          | Medium   |

### Testing + Telemetry Library Additions

| Package                     | Version | Purpose                                              | Category  |
| --------------------------- | ------- | ---------------------------------------------------- | --------- |
| `@axe-core/playwright`      | Latest  | Accessibility testing in E2E                         | Testing   |
| `@lhci/cli`                 | Latest  | Lighthouse CI performance budgets                    | Testing   |
| `@grafana/faro-web-sdk`     | Latest  | Client-side error/trace/log collection               | Telemetry |
| `@grafana/faro-web-tracing` | Latest  | Browser trace instrumentation (fetch, document-load) | Telemetry |
| `web-vitals`                | Latest  | Core Web Vitals collection (optional if using Faro)  | Telemetry |

---

## Appendix: Library Version Matrix

All versions as of March 2026. Pin exact versions per project convention.

| Package                     | Version | Purpose                                          |
| --------------------------- | ------- | ------------------------------------------------ |
| `svelte`                    | 5.53.5  | Already installed                                |
| `@sveltejs/kit`             | 2.53.3  | Already installed                                |
| `zod`                       | 4.3.6   | Already installed                                |
| `libphonenumber-js`         | 1.12.38 | Already installed                                |
| `postcode-validator`        | 3.10.9  | Already installed                                |
| `@stripe/stripe-js`         | 8.8.0   | Already installed                                |
| `tailwindcss`               | 4.2.1   | Already installed                                |
| `clsx`                      | 2.1.1   | Already installed                                |
| `tailwind-merge`            | 3.5.0   | Already installed                                |
| `tailwind-variants`         | 3.2.2   | Already installed                                |
| `tailwindcss-motion`        | 1.1.1   | Already installed                                |
| `sveltekit-superforms`      | 2.29.1  | **To add**                                       |
| `formsnap`                  | 1.0.1   | **To add**                                       |
| `bits-ui`                   | 2.16.2  | **To add** (via shadcn-svelte)                   |
| `svelte-sonner`             | 1.0.7   | **To add** (via shadcn-svelte)                   |
| `intl-tel-input`            | 26.7.5  | **To add**                                       |
| `@tanstack/table-core`      | Latest  | **To add** (via shadcn-svelte data table)        |
| `@internationalized/date`   | Latest  | **To add** (via Bits UI date components)         |
| `date-fns`                  | Latest  | **To add**                                       |
| `better-auth`               | 1.5.x   | **To add** (Stage C)                             |
| `mode-watcher`              | Latest  | **To add** (dark/light mode, shadcn-svelte dep)  |
| `@axe-core/playwright`      | Latest  | **To add** (a11y E2E testing)                    |
| `@lhci/cli`                 | Latest  | **To add** (Lighthouse CI)                       |
| `@grafana/faro-web-sdk`     | Latest  | **To add** (client-side telemetry)               |
| `@grafana/faro-web-tracing` | Latest  | **To add** (browser trace instrumentation)       |
| `web-vitals`                | Latest  | **To add** (Core Web Vitals, optional with Faro) |
