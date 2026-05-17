// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { AuthErrorCodes } from "@d2/auth-abstractions";
import type { Role } from "@d2/auth-context-abstractions";
import { forbidden, HttpStatusCode } from "@d2/result";
import { PROBLEM_DETAILS_CONTENT_TYPE } from "../problem-details.g.js";
import { toProblemDetails } from "../problem-details.js";
import type {
  AuthenticatedRequestContext,
  GuardRequestEvent,
  GuardThrowers,
} from "./guard-types.js";
import { requireAuth } from "./require-auth.js";

/**
 * Asserts the request is authenticated AND the user holds an org role.
 *
 * - With NO `roles` arg: requires only that the user has SOME non-empty
 *   role in the operating org.
 * - With one or more `roles` arg: requires `orgRole` to be one of those.
 *
 * Composes `requireAuth` first.
 */
export function requireRole(
  event: GuardRequestEvent,
  throwers: GuardThrowers,
  ...roles: readonly Role[]
): asserts event is GuardRequestEvent & {
  locals: { requestContext: AuthenticatedRequestContext };
} {
  requireAuth(event, throwers);
  const ctx = event.locals.requestContext;
  if (ctx.orgRole === null) {
    _throwForbidden(event, throwers, ctx.traceId ?? undefined);
  }
  if (roles.length > 0 && !roles.includes(ctx.orgRole as Role)) {
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
  throwers.throwError(
    HttpStatusCode.Forbidden,
    body,
    PROBLEM_DETAILS_CONTENT_TYPE,
  );
}
