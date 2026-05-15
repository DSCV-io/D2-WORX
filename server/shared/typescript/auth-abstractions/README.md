<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/auth-abstractions

Auth-related constants for TS consumers — `Scopes` tree, `AuthErrorCodes`,
`AuthFailures` factories, `JwtClaimTypes`. Mirrors
`D2.Shared.Auth.Abstractions` + `D2.Shared.Auth.Errors` consolidated
(matches the .NET assembly placement).

## Public API

| Export                                                  | Source                            | Mirror                                      |
| ------------------------------------------------------- | --------------------------------- | ------------------------------------------- |
| `Scopes` (nested constants)                             | `scopes.g.ts` (codegen)           | `Scopes.*.*` (.NET)                         |
| `ALL_SCOPES`                                            | `scopes.g.ts`                     | `Scopes.AllScopes`                          |
| `AuthErrorCodes`                                        | `auth-error-codes.g.ts` (codegen) | `D2.Shared.Auth.Errors.AuthErrorCodes`      |
| `ALL_AUTH_ERROR_CODES` / `getAuthErrorHttpStatus(code)` | `auth-error-codes.g.ts`           | `AuthErrorCodes.AllCodes` / `GetHttpStatus` |
| `AuthFailures.<factory>()`                              | `auth-failures.g.ts` (codegen)    | `D2.Shared.Auth.Errors.AuthFailures.*`      |
| `JwtClaimTypes`                                         | `jwt-claim-types.g.ts` (codegen)  | `D2.Shared.Auth.Abstractions.JwtClaimTypes` |
| `JwtPayload`                                            | `jwt-payload.g.ts` (codegen)      | TS-only typed view over the same spec       |

## Codegen workflow

`prebuild` chains 4 emitter scripts (auth-scopes / auth-error-codes /
auth-failures / jwt-claims) before `tsc -b`, so `pnpm -r build` regenerates
transparently. Generated files (`*.g.ts`) are committed to git.

`JwtClaimTypes` AND `JwtPayload` both emit from
`contracts/jwt-claims/jwt-claims.spec.json` via
`tools/ts-codegen/src/jwt-claims-emit.ts` (one runner, two outputs):

- `JwtClaimTypes` — string-constant catalog (every claim's wire name).
- `JwtPayload` — TS interface typed on every `standard` + `d2-custom`
  claim with stable per-field types; `inside-act` claims live nested
  inside `act` and are not surfaced as top-level fields. A trailing
  `raw: Readonly<Record<string, unknown>>` escape hatch carries the
  raw decoded claims for downstream consumers needing access to
  non-spec'd claims.

The .NET side consumes the same spec for `JwtClaimTypes` constants;
.NET reads claim values via `ClaimsPrincipal` so a typed payload is not
needed there. Cross-language drift on the constant catalog is
structurally impossible (single source).

## Header constants

Wire-protocol header constants live in the per-transport catalog
packages — codegen-emitted from `contracts/headers/headers.spec.json`:

- HTTP-applicable headers — `@d2/headers-http`
- AMQP-applicable headers — `@d2/headers-amqp`
- gRPC-applicable headers — `@d2/headers-grpc`
- Cross-transport headers (transport-agnostic consumers) — `@d2/headers-common`

## Dependencies

- `@d2/result` — `D2Result` shape returned by `AuthFailures.*` factories.

## Usage example

```ts
import {
  Scopes,
  AuthFailures,
  AuthErrorCodes,
  JwtClaimTypes,
} from "@d2/auth-abstractions";
import { HttpHeaders } from "@d2/headers-http";

// Scope check.
if (!ctx.scopes.has(Scopes.auth.user.impersonate.consent)) {
  return AuthFailures.scopeInsufficient(ctx.traceId);
}

// Header read.
const idempotency = req.headers[HttpHeaders.IDEMPOTENCY_KEY];

// Claim read.
const sub = jwtPayload[JwtClaimTypes.SUB];

// Error-code discrimination.
if (result.errorCode === AuthErrorCodes.AUTH_JWT_EXPIRED) {
  // refresh the token and retry
}
```

## Parity with .NET

Mirrors `D2.Shared.Auth.Abstractions` + `D2.Shared.Auth.Errors`:

- `Scopes` tree — same dot-segmented spec names emitted as nested
  constants (e.g. `Scopes.auth.user.impersonate.consent`).
- `AuthErrorCodes` — same string values (every constant is its own name).
- `AuthFailures` — every factory returns `D2Result.fail(...)` with
  matching `errorCode` + `statusCode` + default `messageKey`.
- `JwtClaimTypes` — codegen-emitted from `contracts/jwt-claims/jwt-claims.spec.json`;
  same constant names + values on both sides.
- `JwtPayload` — TS-only typed view emitted from the same spec; .NET reads
  claims via `ClaimsPrincipal` rather than a typed shape.

## Edge cases

- `getAuthErrorHttpStatus` returns 500 for unknown codes — defensive
  default, every shipped `AUTH_*` code IS in the table.
- `Scopes` may include `_self` keys at branch nodes when an entry is
  both a leaf scope AND has children (rare; edge case for forward-compat).
- Generated files (`*.g.ts`) are committed to git.
