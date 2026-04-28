import { cors } from "hono/cors";

/**
 * Custom request headers that any middleware in the auth-api pipeline reads.
 * Exported so the security-conformance test can assert CORS `allowHeaders`
 * stays in sync with what the middleware actually consumes — adding a new
 * header read in middleware without listing it here breaks browser preflight.
 */
export const AUTH_CUSTOM_REQUEST_HEADERS = [
  "X-Requested-With",
  // Read by request-enrichment middleware (`enrichRequest`) so the client
  // can supply its hardware/browser fingerprint when the `d2-cfp` cookie
  // isn't available (cross-origin paths).
  "X-Client-Fingerprint",
] as const;

/**
 * CORS middleware configured for allowed origins.
 * Allows credentials (cookies) and standard auth headers.
 */
export function createCorsMiddleware(origins: string[]) {
  return cors({
    origin: origins,
    credentials: true,
    allowHeaders: [
      "Content-Type",
      "Authorization",
      ...AUTH_CUSTOM_REQUEST_HEADERS,
      "traceparent",
      "tracestate",
    ],
    allowMethods: ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
    maxAge: 86400,
  });
}
