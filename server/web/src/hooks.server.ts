import "./instrumentation.server"; // Initialize instrumentation...
import type { Handle, HandleServerError } from "@sveltejs/kit";
import { sequence } from "@sveltejs/kit/hooks";
import { paraglideMiddleware } from "$lib/paraglide/server";
import { requestLogger } from "$lib/server/request-logger.server";
import { logger } from "$lib/server/logger.server";
import { createRequestEnrichmentHandle } from "$lib/server/hooks/request-enrichment.server";
import { createRateLimitHandle } from "$lib/server/hooks/rate-limit.server";
import { createIdempotencyHandle } from "$lib/server/hooks/idempotency.server";
import { createAuthHandle } from "$lib/server/hooks/auth.server";

const handleParaglide: Handle = async ({ event, resolve }) => {
  try {
    requestLogger.info(event, "Request received.", {
      method: event.request.method,
      url: event.url.pathname,
      userAgent: event.request.headers.get("user-agent"),
    });

    const response = await paraglideMiddleware(event.request, ({ request, locale }) => {
      event.request = request;

      return resolve(event, {
        transformPageChunk: ({ html }) => html.replace("%paraglide.lang%", locale),
      });
    });

    requestLogger.info(event, "Request completed.", {
      status: response.status,
      url: event.url.pathname,
    });

    return response;
  } catch (error) {
    requestLogger.error(event, "Request failed", {
      error: error,
      url: event.url.pathname,
    });

    return await resolve(event);
  }
};

// Middleware ordering (mirrors .NET Gateway pipeline):
// 1. Request enrichment (IP, fingerprint, WhoIs)
// 2. Rate limiting (uses requestContext from step 1 — cheap rejection before auth)
// 3. Auth session resolution (resolves session for downstream middleware)
// 4. Idempotency (mutation dedup — auth available for future user-scoped keys)
// 5. Paraglide + request logging
export const handle: Handle = sequence(
  createRequestEnrichmentHandle(),
  createRateLimitHandle(),
  createAuthHandle(),
  createIdempotencyHandle(),
  handleParaglide,
);

export const handleError: HandleServerError = async ({ error, status, message }) => {
  const traceId = crypto.randomUUID();

  const errorMessage = error instanceof Error ? error.message : String(error);
  const errorStack = error instanceof Error ? error.stack : undefined;

  logger.error("Server-side error", {
    error_message: errorMessage,
    error_stack: errorStack,
    error_status: status,
    error_trace_id: traceId,
  });

  return {
    message,
    traceId,
  };
};
