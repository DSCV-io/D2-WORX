<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/headers

SvelteKit BFF-side glue for the BFF↔Edge boundary. Reads the inbound
`Authorization` JWT and `x-d2-context` envelope into an `IRequestContext`
and exposes server-side route guards (`requireAuth`, `requireOrg`,
`requireRole`, `requireScope`, `redirectIfAuthenticated`).

This package does NOT re-export wire-protocol header constants — those
live in `@d2/headers-common` / `@d2/headers-http` / `@d2/headers-grpc`
(codegen-emitted from `contracts/headers/headers.spec.json`). The BFF
hook reads `Authorization` and `x-d2-context` directly via
`@d2/headers-common`; HTTP-specific names are not needed at this layer.

## Public API

| Export                                           | Purpose                                                                                                                                                      |
| ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `parseAuthHeader(authHeader, opts?)`             | Decode `Authorization: Bearer <jwt>` into a `JwtPayload` (re-exported from `@d2/auth-abstractions`). SHAPE-only validation; signature/expiry are Edge's job. |
| `parseRequestContextFromHeaders(headers, opts?)` | Decode Authorization + `x-d2-context` into an `IRequestContext` ready to assign to `event.locals.requestContext`.                                            |
| `toProblemDetails(failure, opts)`                | RFC 7807 ProblemDetails builder; mirrors the .NET `D2ProblemDetailsExtensions` shape.                                                                        |
| `requireAuth(event, throwers)`                   | Asserts authentication. Throws 401 ProblemDetails on failure.                                                                                                |
| `requireOrg(event, throwers, ...types?)`         | Asserts auth + org context (optionally constrained to specific `OrgType`s). Throws 403.                                                                      |
| `requireRole(event, throwers, ...roles?)`        | Asserts auth + non-empty role (optionally constrained). Throws 403.                                                                                          |
| `requireScope(event, throwers, ...scopes)`       | Asserts auth + at least one of the requested scopes (any-of). Throws 403.                                                                                    |
| `redirectIfAuthenticated(event, throwers, to)`   | Bounces sign-in pages to a destination when the user is already authenticated. Throws 303 redirect on the truthy branch.                                     |

## Trust model

The BFF runs on the `internal` overlay — only Edge can reach it. JWT
**signature**, **expiry**, and **audience** validation are Edge's job;
re-validating here would require fetching JWKS from Edge and adds a
trust-layer regression for no security gain. `parseAuthHeader` does
SHAPE-only validation: 3 segments, base64url-decodable, JSON-parseable
claim object, claim types correct.

## Pluggable thrower contract

The 5 guards take a `GuardThrowers` parameter — a tiny interface with
`throwError(status, body)` and `throwRedirect(status, location)`. The
SvelteKit BFF wires SvelteKit's `error()` / `redirect()` into this
interface at composition time; this package itself takes NO SvelteKit
dependency.

## ProblemDetails wire contract

Failure envelopes mirror the .NET
`D2.Shared.Auth.Http.ProblemDetails.D2ProblemDetailsExtensions` shape:

| Field           | Source                                                        |
| --------------- | ------------------------------------------------------------- |
| `type`          | `https://problems.d2-worx.com/{kebab-error-code}`             |
| `title`         | Per-status human-readable title (overridable via opts).       |
| `status`        | HTTP status from the `D2Result`.                              |
| `detail`        | Optional opts.detail.                                         |
| `instance`      | Request URL pathname (mandatory).                             |
| `d2_error_code` | `failure.errorCode` (e.g. `AUTH_BEARER_MISSING`).             |
| `d2_messages`   | `failure.messages` (TKMessage[] for client-side translation). |
| `traceId`       | `failure.traceId` if present.                                 |

Cross-language parity: the extension keys MUST stay in sync with the .NET
emitter — change here, change in `D2ProblemDetailsExtensions`.

## Dependencies

- `@d2/auth-abstractions` — `AuthFailures` semantic factories +
  `JwtClaimTypes` constants + `AuthErrorCodes` + `JwtPayload` typed
  shape (codegen-emitted from `contracts/jwt-claims/jwt-claims.spec.json`).
- `@d2/auth-context-abstractions` — `OrgType` / `Role` enums.
- `@d2/headers-common` — `AUTHORIZATION` / `PROPAGATED_CONTEXT` /
  `TRACEPARENT` / `TRACESTATE` wire-protocol names.
- `@d2/request-context-abstractions` — `IRequestContext` (extends
  `IAuthContext`, so all auth properties are present transitively) +
  `PropagatedContextSerializer.tryDecode()` + `ActorEntry` +
  `OrgType` / `Role` / `ActorKind` / `ImpersonationKind` enums
  (re-exported from `auth-context-abstractions`).
- `@d2/result` — `D2Result` envelope + `HttpStatusCode` constants +
  `fail` / `forbidden` / `ok` factories.
- `@d2/utilities` — `falsey()` for null/empty/whitespace checks.

The `@d2/i18n` Paraglide surface is NOT consumed here: ProblemDetails
titles use the HTTP-protocol-canonical English phrases ("Unauthorized",
"Forbidden", etc.) and `failure.messages` already carry `TKMessage[]`
entries for client-side rendering. The `@d2/logging` `ILogger` is also
not needed: guards never log — silence is the default; the consuming
SvelteKit hook owns request-level diagnostic logs.

## Usage

```ts
import {
  parseRequestContextFromHeaders,
  requireAuth,
  requireOrg,
  requireScope,
  type GuardThrowers,
} from "@d2/headers";
import { error, redirect } from "@sveltejs/kit";
import { Scopes } from "@d2/auth-abstractions";
import { OrgType } from "@d2/auth-context-abstractions";

// SvelteKit hook wiring.
const throwers: GuardThrowers = {
  throwError: (status, body) => error(status, body),
  throwRedirect: (status, location) => redirect(status, location),
};

export const handle: Handle = async ({ event, resolve }) => {
  const decoded = parseRequestContextFromHeaders(event.request.headers, {
    requireAuth: false,
  });
  if (decoded.success) event.locals.requestContext = decoded.data!;
  return resolve(event);
};

// Per-route load.
export const load = async ({ locals, url }) => {
  requireScope(
    { locals, url },
    throwers,
    Scopes.notifications.preferences.write,
  );
  // requestContext is now narrowed to AuthenticatedRequestContext.
  return { userId: locals.requestContext.userId };
};

// Org-typed admin page.
export const adminLoad = async (event) => {
  requireOrg(event, throwers, OrgType.Admin);
};
```

## Edge cases

- `parseAuthHeader` returns `AuthFailures.bearerMissing` when the header
  is null/empty, `AuthFailures.bearerMalformed` for everything else
  (oversized, wrong segment count, non-base64url, non-JSON, malformed
  audience array). Never throws.
- The Authorization header cap is **8 KB** — the industry-standard
  reverse-proxy default (nginx, AWS ALB). Typical D² JWTs are well under
  4 KB; a header longer than 8 KB is either malformed or an
  oversized-token DoS probe.
- `parseRequestContextFromHeaders` with `requireAuth: false` (default)
  returns an unauthenticated `IRequestContext` when no Authorization
  header is present — useful for SSR pages that work both signed-in
  and signed-out (e.g. landing).
- Malformed `x-d2-context` envelope falls through to `null` propagated
  fields silently — fail-soft per platform posture (return empty over
  wrong-data).
- `requireScope` with no scope arg throws HTTP 500 — calling with no
  scopes is a programmer error.
- `redirectIfAuthenticated` validates `to` upfront — empty / non-string
  / contains CR-LF (header injection) throws HTTP 500.
- HTTP header names are case-insensitive per RFC 7230 §3.2; the
  `Headers.get()` API normalizes lookup automatically.

## Tests

Adversarial coverage per platform discipline — every public function has
happy-path + every-rejection-branch + adversarial inputs (oversized
header, malformed JWT shape, non-base64url segments, mixed-case Bearer
scheme, header-injection probes, missing required claims, wrong-type
claims, programmer-error paths). 100/100/100/100 coverage threshold.
