<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2-WORX Code Review Instructions

Microservices SaaS: C# 14 / .NET 10, TypeScript 5.9, Svelte 5. Full conventions in `CLAUDE.md`.

## D2Result Pattern

- **Use semantic factories** — `notFound()`, `unauthorized()`, `forbidden()`, `validationFailed()`, `conflict()`, `serviceUnavailable()`, `unhandledException()`, `cancelled()`, `someFound()`. Never raw `Fail()`/`fail()` with manual statusCode when a factory exists.
- **Never return `ok()` unconditionally after a fallible operation** — if a nested handler or provider can fail, check its result. Use `bubbleFail`/`bubble` to propagate errors.
- **Partial success**: `notFound()` (none) → `someFound()` (partial) → `ok()` (all).

## Handler Rules

- **RedactionSpec required** on any handler touching PII (emails, phones, IPs, addresses, names, message content). Applies to BOTH app AND repo/infra handlers.
- **`validateInput()` BEFORE infrastructure calls** — never let Redis/DB be the first to reject invalid data. Validate at the top of `executeAsync`.
- **Handlers MUST implement their interface** — required for DI registration.
- **Verify DI registration** — after creating a handler, add its registration in the corresponding `registration.ts` or `Extensions.cs`. Missing registrations are silent at compile time.
- **Handler categories**: Query (read-only, no side effects), Command (mutations), Complex (read + side-effect mutations). Don't miscategorize.
- **Verb semantics**: `Find` = resolve/may fetch externally. `Get` = direct lookup by ID.

## TLC/2LC/3LC Layer Convention

- **CQRS 3LC**: `C/` Commands, `Q/` Queries, `U/` Utilities, `X/` Complex.
- **Repository 3LC**: `C/` Create, `R/` Read, `U/` Update, `D/` Delete.
- Interfaces in `Interfaces/{TLC}/Handlers/{3LC}/`. Implementations in `Implementations/` (app) or `{TLC}/Handlers/{3LC}/` (infra).

## Auth & Security

- **Auth flags are `null | boolean`** — `isAuthenticated`, `isTrustedService`, `isOrgEmulating`, `isUserImpersonating`. `null` = not yet determined. Never treat `null` as `false`.
- **IDOR prevention** — derive org/user scope from session/claims, never from user-supplied input.
- **API key comparisons must be constant-time** — `CryptographicOperations.FixedTimeEquals` (.NET), `timingSafeEqual` (Node.js).
- **New JWT claims** → add to BOTH `JWT_CLAIM_TYPES` (Node.js) and `JwtClaimTypes` (.NET).

## TypeScript

- **`.d.ts` files in `src/` are NOT emitted to `dist/`** — module augmentations must be in `.ts` files.

## SvelteKit

- **All user-visible strings use Paraglide** — `m.key_name()` from `$lib/paraglide/messages.js`. Never hardcode text, even for dev pages. Includes `<title>`, meta tags, labels, placeholders, errors.
- **Never bare `href="/path"` or `goto("/path")`** — always wrap with `resolve("/path")` from `$app/paths`.
- **Client telemetry must never include PII** — Faro identity limited to `userId` + `username`.

## C# Specifics

- **`Falsey()`/`Truthy()` handle null** — never `if (value is null || value.Falsey())`. After early return, use `value!`.
- **`string.Empty`** — never `""`.
- **Extension members**: C# 14 `extension(T)` syntax, not old `this T` parameter style.
- **Field prefixes**: `_camelCase` (mutable), `r_camelCase` (readonly), `s_camelCase` (static), `sr_camelCase` (static readonly).

## i18n

Backend D2Result `messages` and `inputErrors` are user-visible — they need translation keys too, not just frontend strings. New keys must be added to ALL locale files in `contracts/messages/`.

## What NOT to Flag

- Build warnings, lint, formatting — CI enforces these.
- Missing tests — CI runs test suites.
- C# 14 `extension` keyword syntax — this is valid (see `.github/instructions/csharp.instructions.md`).
