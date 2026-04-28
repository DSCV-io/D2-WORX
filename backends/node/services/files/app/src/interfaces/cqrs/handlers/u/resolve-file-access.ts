import type { IHandler } from "@d2/handler";
import type { ContextKeyConfig } from "../../../../context-key-config.js";

export interface ResolveFileAccessInput {
  readonly config: ContextKeyConfig;
  /**
   * - `upload` → uses `config.uploadResolution`
   * - `read`   → uses `config.readResolution` (fetch one by id; permits the
   *              broader `authenticated` strategy for public-by-id files)
   * - `list`   → uses `config.listResolution` (enumerate the collection;
   *              `authenticated` is intentionally excluded at the type level
   *              to prevent cross-tenant enumeration)
   */
  readonly action: "upload" | "read" | "list";
  readonly relatedEntityId: string;
}

export type ResolveFileAccessOutput = undefined;

/**
 * Resolves file access based on the context key's resolution strategy.
 *
 * Uses the request context from IHandlerContext (jwt_owner, jwt_org,
 * authenticated) or delegates to the outbound CheckFileAccess handler
 * for callback resolution. Fail-closed: unknown resolution → forbidden.
 */
export type IResolveFileAccessHandler = IHandler<ResolveFileAccessInput, ResolveFileAccessOutput>;
