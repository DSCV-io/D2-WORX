import type { ServiceScope } from "@d2/di";
import type { IRequestContext } from "@d2/handler";
import type { ILogger } from "@d2/logging";

/**
 * Hono context variable keys — centralized for maintainability.
 */
export const SCOPE_KEY = "scope" as const;
export const REQUEST_CONTEXT_KEY = "requestContext" as const;
export const REQUEST_LOGGER_KEY = "requestLogger" as const;
/** Enriched context from request-enrichment middleware (preserved before JWT overwrites). */
export const ENRICHED_CONTEXT_KEY = "enrichedContext" as const;

/**
 * Strongly-typed Hono `c.var` shape published by the Files API middleware
 * pipeline. Routes declare `new Hono<{ Variables: FilesVariables }>()` so
 * `c.var.scope`, `c.var.requestContext`, etc. type-check without `any` casts.
 *
 * Mirrors auth-api's `SessionVariables` + `ScopeVariables` pattern.
 */
export interface FilesVariables {
  [SCOPE_KEY]: ServiceScope;
  [REQUEST_CONTEXT_KEY]: IRequestContext;
  [REQUEST_LOGGER_KEY]?: ILogger;
  [ENRICHED_CONTEXT_KEY]?: IRequestContext;
}
