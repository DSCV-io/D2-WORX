// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { IRequestContext } from "@d2/request-context-abstractions";
import type { ProblemDetailsBody } from "../problem-details.js";

/**
 * Minimal SvelteKit-compatible request-event shape. Defined locally so
 * `@d2/headers` does not depend on `@sveltejs/kit` directly — the BFF
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
 * `@d2/headers` without taking on a SvelteKit dep. Consumers wire in
 * their own thrower (typically by re-exporting SvelteKit's helpers).
 *
 * BOTH methods MUST throw — they never return. The `never` return
 * type lets the guards' `asserts` narrowing work as expected.
 */
export interface GuardThrowers {
  /** Throws an HTTP error with the given ProblemDetails body. */
  throwError(status: number, body: ProblemDetailsBody): never;
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
