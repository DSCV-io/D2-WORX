// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { AuthErrorCodes } from "@d2/auth-abstractions";
import type { OrgType } from "@d2/auth-context-abstractions";
import { forbidden, HttpStatusCode } from "@d2/result";
import { falsey } from "@d2/utilities";
import { PROBLEM_DETAILS_CONTENT_TYPE } from "../problem-details.g.js";
import { toProblemDetails } from "../problem-details.js";
import type {
  AuthenticatedRequestContext,
  GuardRequestEvent,
  GuardThrowers,
} from "./guard-types.js";
import { requireAuth } from "./require-auth.js";

/**
 * Asserts the request is authenticated AND has an active org context.
 *
 * - With NO `types` arg: requires only that an org context is present
 *   (any non-empty `orgId`).
 * - With one or more `types` arg: requires that `orgType` matches one
 *   of the given types.
 *
 * Composes `requireAuth` first — single source of truth for the auth
 * boundary. A non-authenticated request gets a 401 (from `requireAuth`),
 * not a 403 — the auth-bearer check fires first.
 */
export function requireOrg(
  event: GuardRequestEvent,
  throwers: GuardThrowers,
  ...types: readonly OrgType[]
): asserts event is GuardRequestEvent & {
  locals: { requestContext: AuthenticatedRequestContext };
} {
  requireAuth(event, throwers);
  const ctx = event.locals.requestContext;
  if (falsey(ctx.orgId)) {
    _throwForbidden(event, throwers, ctx.traceId ?? undefined);
  }
  if (types.length > 0) {
    const orgType = ctx.orgType;
    if (orgType === null || !types.includes(orgType)) {
      _throwForbidden(event, throwers, ctx.traceId ?? undefined);
    }
  }
}

function _throwForbidden(
  event: GuardRequestEvent,
  throwers: GuardThrowers,
  traceId: string | undefined,
): never {
  // RFC 7807 §3.1: ProblemDetails body.status MUST equal the HTTP status.
  // The `AuthFailures.scopeInsufficient` factory deliberately surfaces as
  // 401 (uniform-shape rationale on the .NET side); guards run AFTER
  // requireAuth so the bearer is known good — the failure here is an
  // authorization (scope/org) check, semantically a 403. Use the
  // `forbidden()` factory with the `AUTH_SCOPE_INSUFFICIENT` errorCode
  // so HTTP status and body.status agree at 403.
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
