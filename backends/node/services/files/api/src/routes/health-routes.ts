import { Hono } from "hono";
import type { ServiceProvider } from "@d2/di";
import { createServiceScope } from "@d2/handler";
import { ICheckHealthKey } from "@d2/files-app";

/**
 * Health check routes (public, no auth required).
 *
 * - `GET /health` — full health check with component statuses
 * - `GET /ready` — readiness probe (same as health)
 */
export function createHealthRoutes(provider: ServiceProvider): Hono {
  const app = new Hono();

  const handleHealthCheck = async () => {
    const scope = createServiceScope(provider);
    try {
      const handler = scope.resolve(ICheckHealthKey);
      const result = await handler.handleAsync({});
      const status = result.data?.status ?? "unhealthy";
      const httpStatus = status === "healthy" ? 200 : 503;
      return new Response(
        JSON.stringify({
          success: result.success,
          statusCode: httpStatus,
          data: result.data,
          messages: result.messages,
        }),
        {
          status: httpStatus,
          headers: { "Content-Type": "application/json" },
        },
      );
    } finally {
      scope.dispose();
    }
  };

  app.get("/health", () => handleHealthCheck());
  app.get("/ready", () => handleHealthCheck());

  return app;
}
