/**
 * SvelteKit Handle wrapper for idempotency.
 * Mirrors the checkIdempotency() orchestrator pattern from @d2/idempotency.
 *
 * Only applies to mutation methods (POST, PUT, PATCH, DELETE).
 * Reads Idempotency-Key header, checks Redis via SET NX, returns cached
 * response on replay or stores new response after downstream completes.
 */
import type { Handle } from "@sveltejs/kit";
import { TK } from "@d2/i18n/keys";
import { getMiddlewareContext, checkIdempotency } from "../middleware.server";

const MUTATION_METHODS = new Set(["POST", "PUT", "PATCH", "DELETE"]);

export function createIdempotencyHandle(): Handle {
  return async ({ event, resolve }) => {
    const ctx = getMiddlewareContext();
    if (!ctx) return resolve(event);

    if (!MUTATION_METHODS.has(event.request.method)) return resolve(event);

    const idempotencyKey = event.request.headers.get("idempotency-key");
    if (!idempotencyKey) return resolve(event);

    // Scope by userId to prevent cross-user cache collisions.
    // Auth runs before idempotency in the middleware chain, so session is available.
    const scopeId = event.locals.session?.userId;

    const result = await checkIdempotency(
      idempotencyKey,
      ctx.idempotencyCheck,
      ctx.redisSet,
      ctx.redisRemove,
      undefined,
      ctx.logger,
      scopeId,
    );

    // Return cached response on replay.
    if (result.state === "cached" && result.cachedResponse) {
      const contentType = result.cachedResponse.contentType ?? "application/json";
      return new Response(result.cachedResponse.body, {
        status: result.cachedResponse.statusCode,
        headers: { "Content-Type": contentType },
      });
    }

    // Another request with the same key is still in flight.
    if (result.state === "in_flight") {
      return new Response(
        JSON.stringify({
          success: false,
          messages: [TK.common.errors.CONFLICT],
          statusCode: 409,
          errorCode: "IDEMPOTENCY_IN_FLIGHT",
        }),
        {
          status: 409,
          headers: { "Content-Type": "application/json" },
        },
      );
    }

    // state === "acquired" — proceed with the request, then cache the response.
    const response = await resolve(event);

    const clone = response.clone();
    const body = await clone.text();
    await result.storeResponse({
      statusCode: clone.status,
      body,
      contentType: clone.headers.get("content-type") ?? undefined,
    });

    return response;
  };
}
