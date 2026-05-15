// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { AuthErrorCodes } from "@d2/auth-abstractions";
import { fail, forbidden, HttpStatusCode } from "@d2/result";
import { toProblemDetails } from "../problem-details.js";
import type {
  AuthenticatedRequestContext,
  GuardRequestEvent,
  GuardThrowers,
} from "./guard-types.js";
import { requireAuth } from "./require-auth.js";

/**
 * Asserts the request is authenticated AND its token holds at least
 * ONE of the requested scopes (any-of semantics, mirroring the .NET
 * `JwtAuthMiddleware.RequestContextHasAnyScope`).
 *
 * Calling with no scopes is a programmer error — the call is meaningless.
 * In that case throws an HTTP 500 ProblemDetails with `UNHANDLED_EXCEPTION`
 * to make the misuse visible in logs / observability.
 */
export function requireScope(
  event: GuardRequestEvent,
  throwers: GuardThrowers,
  ...scopes: readonly string[]
): asserts event is GuardRequestEvent & {
  locals: { requestContext: AuthenticatedRequestContext };
} {
  if (scopes.length === 0) {
    // Programmer error — calling requireScope() with no scopes is meaningless.
    const ctx = event.locals.requestContext;
    const traceId = ctx?.traceId ?? undefined;
    const failure = fail({
      statusCode: HttpStatusCode.InternalServerError,
      errorCode: "REQUIRE_SCOPE_NO_ARGS",
      traceId,
    });
    const body = toProblemDetails(failure, {
      instance: event.url.pathname,
      title: "requireScope misuse",
    });
    throwers.throwError(HttpStatusCode.InternalServerError, body);
  }
  requireAuth(event, throwers);
  const ctx = event.locals.requestContext;
  const set = ctx.scopes;
  if (!(set instanceof Set)) {
    _throwForbidden(event, throwers, ctx.traceId ?? undefined);
  }
  let hasAny = false;
  for (const s of scopes) {
    if (set.has(s)) {
      hasAny = true;
      break;
    }
  }
  if (!hasAny) {
    _throwForbidden(event, throwers, ctx.traceId ?? undefined);
  }
}

function _throwForbidden(
  event: GuardRequestEvent,
  throwers: GuardThrowers,
  traceId: string | undefined,
): never {
  // RFC 7807 §3.1: body.status MUST equal HTTP status. The
  // `AuthFailures.scopeInsufficient` factory surfaces as 401 by design
  // for the auth boundary; this guard runs AFTER requireAuth so the
  // failure is semantically a 403. Use `forbidden()` with the
  // `AUTH_SCOPE_INSUFFICIENT` errorCode so the two values agree.
  const failure = forbidden({
    errorCode: AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT,
    traceId,
  });
  const body = toProblemDetails(failure, { instance: event.url.pathname });
  throwers.throwError(HttpStatusCode.Forbidden, body);
}
