// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fail, HttpStatusCode } from "@d2/result";
import { falsey } from "@d2/utilities";
import { PROBLEM_DETAILS_CONTENT_TYPE } from "@d2/problem-details-abstractions";
import { toProblemDetails } from "../problem-details.js";
import type { GuardRequestEvent, GuardThrowers } from "./guard-types.js";

/**
 * Sign-in / sign-up page bouncer. When the user is already authenticated,
 * redirects (303 See Other) to `to`. Otherwise returns void.
 *
 * Per HTTP spec, 303 is the right status for a post-state-change redirect
 * to a GET resource — matches SvelteKit's `redirect()` default semantics.
 *
 * Validates `to` upfront — empty / non-string / contains CR-LF (header
 * injection) is a programmer error and throws an HTTP 500.
 */
export function redirectIfAuthenticated(
  event: GuardRequestEvent,
  throwers: GuardThrowers,
  to: string,
): void {
  if (typeof to !== "string" || falsey(to) || /[\r\n]/.test(to)) {
    const ctx = event.locals.requestContext;
    const traceId = ctx?.traceId ?? undefined;
    const failure = fail({
      statusCode: HttpStatusCode.InternalServerError,
      errorCode: "REDIRECT_INVALID_TARGET",
      traceId,
    });
    const body = toProblemDetails(failure, {
      instance: event.url.pathname,
      title: "redirectIfAuthenticated invalid target",
    });
    throwers.throwError(
      HttpStatusCode.InternalServerError,
      body,
      PROBLEM_DETAILS_CONTENT_TYPE,
    );
  }
  const ctx = event.locals.requestContext;
  if (ctx === undefined || ctx === null) return;
  if (ctx.isAuthenticated === true) {
    throwers.throwRedirect(303, to);
  }
}
