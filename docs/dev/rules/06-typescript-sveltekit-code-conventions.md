<!--
Copyright (c) DCSV. All rights reserved.
-->

## 6. TypeScript / SvelteKit Code Conventions
<a name="top"></a>
_[← rules index](../rules.md) · §6 of the D2-WORX rules catalog._

**Predicate index:** §6.1–§6.15 · 15 predicates.

### Predicates — §6 TypeScript / SvelteKit conventions

- **6.1** Is TypeScript `strict` mode enabled in every `tsconfig.json`?
  - Evidence: per `tsconfig.json` touched → `"strict": true` confirmed.

- **6.2** Are type-only imports using `import type { ... }` syntax?
  - Evidence: per import touching only types → `import type` form.

- **6.3** Is `undefined` preferred over `null` for "absent" semantics? Use optional syntax (`field?: string`) instead of `field: string | null`.
  - **Exception**: explicit three-state semantics for pre-auth flags (`boolean | null`).
  - Evidence: per new optional field → `?: T` form (not `T | null`).

- **6.4** Is `truthyOrUndefined()` used at boundaries (user input, DB rows, proto values → domain types)? Returns `undefined` if the string is null, empty, or whitespace-only.
  - Evidence: per boundary → call confirmed.

- **6.5** Do Zod schemas use `.optional()` (not `.nullable()` / `.nullish()`) for domain-aligned validation?
  - **Why**: domain types use `?: T` (undefined), so Zod must match.
  - Evidence: per new Zod schema → `.optional()` form.

- **6.6** Does `pnpm exec svelte-check` produce zero errors / warnings in `server/web/`?
  - Evidence: command output.

- **6.7** Does `pnpm exec eslint .` produce zero warnings in `server/web/`?
  - Evidence: command output.

- **6.8** Does `pnpm exec prettier --check .` produce zero formatting failures in `server/web/`?
  - Evidence: command output.

- **6.9** Were diagnostics checked via `mcp__cclsp__get_diagnostics` after every TS edit?
  - Evidence: tool-call history shows diagnostic checks.

### SvelteKit BFF specifics

- **6.10** Are REST client modules the ONLY place that calls `fetch`? Components and pages call client functions, never `fetch("/api/...")` directly.
  - **Layout**: `$lib/client/rest/*-client.ts` modules expose per-feature client API and own credentials / headers / timeouts; `$lib/shared/rest/` holds isomorphic low-level helpers (e.g. `gateway-response.ts` — gateway response parser used by both server-side and browser-side clients). Raw `fetch()` allowed inside `$lib/shared/rest/` helpers AND inside `*-client.ts` files; NOT in components, pages, or any other path.
  - Evidence: `grep -rEn 'fetch\(' <scope>` → per hit, classify (allowed/forbidden).

- **6.11** Async / server-loaded data shows a `<Skeleton>` placeholder until ready. See §19.1 for the canonical predicate (UX category is the natural home). This row is the §6 cross-pointer; walk §19.1 for evidence.

- **6.12** Navigation must use `resolve("/path")` from `$app/paths` (not bare `href="/path"` / `goto("/path")`). See §12.7 for the canonical predicate — i18n locale routing is the underlying reason. This row is the §6 cross-pointer; walk §12.7 for evidence.

- **6.13** Are query strings appended outside the typed pathname call? `` `${resolve("/path")}?key=value` `` (NOT inside `resolve(...)`).
  - Evidence: per query-string usage → form confirmed.

- **6.14** Is the SvelteKit BFF pure SSR? (Browser → Edge directly for auth state mutations. Server-side route guards (`requireAuth`, `requireOrg`, etc.) at `server/web/src/lib/server/auth/`. Browser-side `authClient` at `server/web/src/lib/client/auth/`.)
  - Evidence: per new auth surface → location confirmed.

- **6.15** Are TypeScript optional fields declared with the shorthand `field?: T` rather than the explicit union `field: T | undefined`? Are `T | null` unions absent from all interface fields, function return types, and local variables?
  - **Shorthand `field?: T` required on interface fields**: `field: T | undefined` on an interface field is forbidden — use `field?: T`. The shorthand is more permissive at the call site (callers can omit the field entirely, not just pass `undefined` explicitly) and matches the C# `T?` shorthand convention (§5 + §7).
  - **`T | null` forbidden everywhere**: use `T | undefined` for return types / local variables where "explicitly absent" is the intended semantic, or `field?: T` for optional interface fields. `null` is a distinct value requiring an explicit branch (`=== null`) and has no place in domain types — `undefined` satisfies the "absent" semantic universally.
  - **Allowed — `T | undefined` on function return types**: when the precise semantic is "this function explicitly returns `undefined` on failure / absent, NOT optional parameter omission," the explicit `T | undefined` union on a return type or local variable is acceptable. Example: `resolve(input: string): CountryCode | undefined` (clearly signals "caller must handle the undefined return path").
  - **`boolean | null` exception** (per §6.3): explicit three-state semantics for pre-auth flags only.
  - **Language-forced `T | null` exception**: a `T | null` type that is mandated by a built-in language/runtime API's return type is exempt — there is no idiomatic `undefined`-typed alternative. Canonical example: `RegExpExecArray | null` from `RegExp.prototype.exec()`, required by the `while ((m = re.exec(s)) !== null)` loop idiom. The §6.15 ban targets AUTHORED types, not language-forced return types.
  - **Evidence**: `grep -rEn ": [A-Za-z][A-Za-z0-9<>, ]* \| undefined" server/shared/typescript/**/*.ts` on interface field lines → zero matches expected (interface fields must use `?:` form). `grep -rEn ": [A-Za-z][A-Za-z0-9<>, ]* \| null" server/shared/typescript/**/*.ts` → zero matches expected (excluding the `boolean | null` pre-auth exception and language-forced built-in return types).
  - **Why**: matches C#'s `T?` shorthand + call-site ergonomics — `field?: T` callers can omit the field in object literals, while `field: T | undefined` forces `field: undefined` everywhere. JSON-wire `null` from .NET nullable value types normalizes to `undefined` at the Zod boundary, so `T | null` in domain types creates a false three-state expectation.
  - **How to apply**: refactor `field: T | undefined` on interfaces → `field?: T` and audit call sites (they may simplify `{ field: undefined }` → `{}`). For JSDoc on optional fields use `{T} [field]` (not `{T|undefined}`).

<sup>[↑ jump to top](#top)</sup>

---

