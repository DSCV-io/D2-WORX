import { createHash } from "node:crypto";
import { Hono } from "hono";
import type { Context } from "hono";
import { D2Result, HttpStatusCode } from "@d2/result";
import { TK } from "@d2/i18n";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import type { ILogger } from "@d2/logging";
import type { Auth } from "@d2/auth-infra";
import { signUpPrefsStorage } from "@d2/auth-infra";
import type { CheckSignInThrottle, RecordSignInOutcome } from "@d2/auth-app";
import { REQUEST_CONTEXT_KEY } from "../middleware/request-enrichment.js";
import { getCookie } from "hono/cookie";

/**
 * Clones response headers and sets a single header (overwrites if exists).
 */
function setHeader(original: Headers, key: string, value: string): Headers {
  const headers = new Headers(original);
  headers.set(key, value);
  return headers;
}

function sha256(input: string): string {
  return createHash("sha256").update(input, "utf8").digest("hex");
}

/**
 * Extracts the sign-in identifier from the request body.
 * Returns lowercase email or username, or undefined if not found.
 */
function extractIdentifier(body: Record<string, unknown> | null, path: string): string | undefined {
  if (!body) return undefined;
  if (path.endsWith("/sign-in/email")) return (body.email as string)?.toLowerCase?.();
  if (path.endsWith("/sign-in/username")) return (body.username as string)?.toLowerCase?.();
  return undefined;
}

/**
 * Audit-record callback for failed sign-in attempts. Receives the raw identifier
 * (email or username), the request envelope, and the BetterAuth response status.
 * Implementation lives in the composition root so it can resolve handlers + the
 * RabbitMQ publisher with full DI context.
 */
export interface RecordFailedSignIn {
  (data: {
    email?: string;
    username?: string;
    ipAddress: string;
    userAgent: string;
    deviceFingerprint?: string;
    failureReason: string;
  }): Promise<void>;
}

/**
 * Mounts BetterAuth at /api/auth/*.
 *
 * Sign-in endpoints (`/sign-in/email`, `/sign-in/username`) are guarded by
 * the optional throttle handlers — progressive delay per (identifier, identity).
 *
 * All other BetterAuth endpoints are passed through to auth.handler directly.
 */
export function createAuthRoutes(
  auth: Auth,
  throttleHandlers?: { check: CheckSignInThrottle; record: RecordSignInOutcome },
  logger?: ILogger,
  recordFailedSignIn?: RecordFailedSignIn,
) {
  const app = new Hono();

  // Populate sign-up preferences from cookies so the databaseHooks
  // user.create.before hook can read locale + timezone.
  app.use("*", async (c, next) => {
    const locale = getCookie(c, "PARAGLIDE_LOCALE");
    const timezone = getCookie(c, "D2_TIMEZONE");
    return signUpPrefsStorage.run({ locale, timezone }, next);
  });

  /**
   * Shared sign-in handler with optional throttle guard + audit recording.
   *
   * 1. Clone body to extract identifier (email or username)
   * 2. Check throttle → 429 if blocked
   * 3. Forward to BetterAuth
   * 4. Record throttle outcome (fire-and-forget)
   * 5. On non-200 response, record a failed `sign_in_event` row (fire-and-forget)
   *
   * Successful sign-ins are recorded by `databaseHooks.session.create.after` →
   * `onSignIn` callback, which has the actual session/user object.
   */
  const handleSignIn = async (c: Context) => {
    // Clone the request so BetterAuth can still read the body. We need this for
    // both throttle (identifier) and audit-on-failure (resolve userId on 401).
    const body = await c.req.raw
      .clone()
      .json()
      .catch(() => null);
    const identifier = extractIdentifier(body as Record<string, unknown> | null, c.req.path);
    const isEmailEndpoint = c.req.path.endsWith("/sign-in/email");

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const requestContext = (c as any).get(REQUEST_CONTEXT_KEY) as
      | {
          clientIp?: string;
          serverFingerprint?: string;
          deviceFingerprint?: string;
        }
      | undefined;
    const userAgent = c.req.header("user-agent") ?? "unknown";

    if (throttleHandlers && identifier) {
      const identifierHash = sha256(identifier);
      const identityHash = sha256(
        `${requestContext?.clientIp ?? "unknown"}:${requestContext?.serverFingerprint ?? "unknown"}`,
      );

      // Check throttle
      const check = await throttleHandlers.check.handleAsync({ identifierHash, identityHash });
      if (check.success && check.data?.blocked) {
        const retryAfterSec = check.data.retryAfterSec ?? 300;
        c.header("Retry-After", String(retryAfterSec));
        return c.json(
          D2Result.fail({
            messages: [TK.auth.errors.SIGN_IN_THROTTLED],
            statusCode: HttpStatusCode.TooManyRequests,
            errorCode: "SIGN_IN_THROTTLED",
          }),
          429 as ContentfulStatusCode,
        );
      }

      // Forward to BetterAuth
      const response = await auth.handler(c.req.raw);

      // Record throttle outcome (fire-and-forget — don't block the response)
      throttleHandlers.record
        .handleAsync({
          identifierHash,
          identityHash,
          responseStatus: response.status,
        })
        .catch((err: unknown) =>
          logger?.warn("Throttle record failed (non-blocking)", {
            error: err instanceof Error ? err.message : String(err),
          }),
        );

      if (response.status !== 200) {
        if (logger) {
          logger.warn("Sign-in attempt failed", {
            path: c.req.path,
            status: response.status,
            identifierHash,
          });
        }
        // Audit failed sign-in (fire-and-forget). Skipped silently if no userId
        // resolves (attacker probing nonexistent emails) — the throttle layer
        // still tracks those by hashed identifier.
        recordFailedSignIn?.({
          email: isEmailEndpoint ? identifier : undefined,
          username: isEmailEndpoint ? undefined : identifier,
          ipAddress: requestContext?.clientIp ?? "unknown",
          userAgent,
          deviceFingerprint: requestContext?.deviceFingerprint,
          failureReason: `http_${response.status}`,
        }).catch((err: unknown) =>
          logger?.warn("recordFailedSignIn failed (non-blocking)", {
            error: err instanceof Error ? err.message : String(err),
          }),
        );
      }

      return response;
    }

    // No throttle or no identifier found — pass through to BetterAuth
    return auth.handler(c.req.raw);
  };

  // Sign-in endpoints with throttle guard
  app.post("/api/auth/sign-in/email", handleSignIn);
  app.post("/api/auth/sign-in/username", handleSignIn);

  // Catch-all for other BetterAuth routes
  app.all("/api/auth/*", async (c) => {
    const response = await auth.handler(c.req.raw);

    // Add Cache-Control on JWKS/discovery responses.
    // Keys rotate every 30 days — 1 hour cache is conservative.
    // Reduces upstream fetches from .NET gateway and intermediate proxies.
    if (c.req.path.includes(".well-known/")) {
      return new Response(response.body, {
        status: response.status,
        headers: setHeader(response.headers, "Cache-Control", "public, max-age=3600"),
      });
    }

    return response;
  });

  return app;
}
