// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { IRequestContext } from "@dcsv-io/d2-request-context-abstractions";
import type { ProblemDetailsBody } from "../problem-details.js";

/**
 * Minimal SvelteKit-compatible request-event shape. Defined locally so
 * `@dcsv-io/d2-headers` does not depend on `@sveltejs/kit` directly — the BFF
 * passes its `RequestEvent`-typed value at the call site; structural
 * typing makes the SvelteKit type assignable to this one.
 */
export interface GuardRequestEvent {
  readonly url: { readonly pathname: string };
  // `locals` is mutable by design (SvelteKit hook populates it); but
  // this guard surface only reads `requestContext`.
  locals: { requestContext?: IRequestContext };
}

/**
 * Pluggable thrower contract. SvelteKit's `error()` / `redirect()` are
 * runtime functions that throw — they are not importable from
 * `@dcsv-io/d2-headers` without taking on a SvelteKit dep. Consumers wire in
 * their own thrower (typically by re-exporting SvelteKit's helpers).
 *
 * BOTH methods MUST throw — they never return. The `never` return
 * type lets the guards' `asserts` narrowing work as expected.
 *
 * `throwError` is invoked by every guard with a `ProblemDetailsBody`
 * payload and the `application/problem+json` content type (re-exported
 * from `@dcsv-io/d2-headers` as `PROBLEM_DETAILS_CONTENT_TYPE` — RFC 7807 §6.1
 * SHOULD compliance). Implementations MUST honor the supplied
 * `contentType` on the outbound response (e.g. by constructing a
 * SvelteKit `Response` with the header set) — defaulting to
 * `application/json` strips ProblemDetails-aware clients of the
 * spec-defined content discriminator.
 */
export interface GuardThrowers {
  /**
   * Throws an HTTP error with the given ProblemDetails body. Implementations
   * MUST set the response Content-Type to the supplied `contentType` value
   * (always `application/problem+json` for guard-issued rejections).
   */
  throwError(
    status: number,
    body: ProblemDetailsBody,
    contentType: string,
  ): never;
  /** Throws a redirect to the given URL. */
  throwRedirect(status: number, location: string): never;
}

/**
 * Type narrowing alias — when a guard passes `requireAuth`, the request
 * context is known to be present + authenticated.
 */
export type AuthenticatedRequestContext = IRequestContext & {
  readonly isAuthenticated: true;
};
