import type { Context } from "hono";
import type { IRequestContext } from "@d2/handler";
import { REQUEST_CONTEXT_KEY } from "./constants.js";

/**
 * Reads the `IRequestContext` populated by upstream auth middleware. Returns
 * `undefined` if no auth middleware has run yet — every `require*` middleware
 * treats that the same as "unauthenticated" and 401s the request.
 */
export function readRequestContext(c: Context): IRequestContext | undefined {
  return c.get(REQUEST_CONTEXT_KEY) as IRequestContext | undefined;
}
