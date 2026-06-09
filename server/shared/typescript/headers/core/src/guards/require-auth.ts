// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { AuthFailures } from "@d2/auth-abstractions";
import { HttpStatusCode } from "@d2/result";
import { PROBLEM_DETAILS_CONTENT_TYPE } from "@d2/problem-details-abstractions";
import { toProblemDetails } from "../problem-details.js";
import type {
  AuthenticatedRequestContext,
  GuardRequestEvent,
  GuardThrowers,
} from "./guard-types.js";

/**
 * Asserts that the request is authenticated. Throws a SvelteKit `error(401)`
 * via the injected thrower with an RFC 7807 ProblemDetails body when not.
 *
 * On the success branch, the call site can read
 * `event.locals.requestContext.userId` etc. without `?? ""` because the
 * `asserts` narrowing tells the type system the context is present and
 * `isAuthenticated === true`.
 */
export function requireAuth(
  event: GuardRequestEvent,
  throwers: GuardThrowers,
): asserts event is GuardRequestEvent & {
  locals: { requestContext: AuthenticatedRequestContext };
} {
  const ctx = event.locals.requestContext;
  if (
    ctx === undefined ||
    ctx === null ||
    typeof (ctx as { isAuthenticated: unknown }).isAuthenticated !==
      "boolean" ||
    ctx.isAuthenticated !== true
  ) {
    const traceId = ctx?.traceId ?? undefined;
    const failure = AuthFailures.bearerMissing(traceId);
    const body = toProblemDetails(failure, { instance: event.url.pathname });
    throwers.throwError(
      HttpStatusCode.Unauthorized,
      body,
      PROBLEM_DETAILS_CONTENT_TYPE,
    );
  }
}
