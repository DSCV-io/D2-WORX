import type { Context, MiddlewareHandler } from "hono";
import type { JWTVerifyGetKey } from "jose";
import type { IRequestContext } from "@d2/handler";
import { createJwksProvider } from "./jwks-provider.js";
import { verifyToken } from "./verify-token.js";
import { checkFingerprint } from "./fingerprint-check.js";
import { populateRequestContext } from "./populate-context.js";

export interface JwtAuthOptions {
  /** URL of the JWKS endpoint (e.g., "http://d2-auth:5100/api/auth/jwks"). */
  readonly jwksUrl: string;
  /** Expected JWT issuer (e.g., "d2-worx"). */
  readonly issuer: string;
  /** Expected JWT audience (e.g., "d2-services"). */
  readonly audience: string;
  /** Enable fingerprint validation (SHA-256(UA|Accept) check). Default: true. */
  readonly fingerprintCheck?: boolean;
}

/**
 * Hono middleware that validates JWT Bearer tokens against a remote JWKS.
 *
 * On success, populates `c.set("requestContext", ...)` with IRequestContext
 * derived from JWT claims. On failure, returns a 401 JSON response matching
 * the D2Result error shape.
 *
 * The JWKS provider is created once at middleware initialization and reused
 * across all requests (jose handles key rotation and caching internally).
 */
export function jwtAuth(options: JwtAuthOptions): MiddlewareHandler {
  const jwks: JWTVerifyGetKey = createJwksProvider(options.jwksUrl);
  const enableFingerprint = options.fingerprintCheck !== false;

  return async (c: Context, next) => {
    // Extract Bearer token from Authorization header
    const authHeader = c.req.header("authorization");
    if (!authHeader) {
      return unauthorizedResponse(c, "Missing Authorization header.");
    }

    const [scheme, token] = authHeader.split(" ", 2);
    if (scheme?.toLowerCase() !== "bearer" || !token) {
      return unauthorizedResponse(
        c,
        "Invalid Authorization header format. Expected: Bearer <token>",
      );
    }

    // Verify token signature, expiry, issuer, audience
    const verifyResult = await verifyToken(token, {
      jwks,
      issuer: options.issuer,
      audience: options.audience,
    });

    if (!verifyResult.success) {
      return unauthorizedResponse(c, "Unauthorized");
    }

    // Safe: success === true guarantees data is defined
    const { payload } = verifyResult.data!;

    // Fingerprint check (if enabled)
    if (enableFingerprint) {
      const userAgent = c.req.header("user-agent") ?? "";
      const accept = c.req.header("accept") ?? "";
      const fpResult = await checkFingerprint(payload, userAgent, accept);
      if (!fpResult.success) {
        return unauthorizedResponse(c, "Unauthorized");
      }
    }

    // Populate IRequestContext from JWT claims, merging with any upstream context
    // (e.g., clientIp, traceId, serverFingerprint from request enrichment middleware)
    const existingContext = c.get("requestContext") as Partial<IRequestContext> | undefined;
    const jwtContext = populateRequestContext(payload);
    const mergedContext: IRequestContext = { ...existingContext, ...jwtContext };
    c.set("requestContext", mergedContext);

    await next();
  };
}

function unauthorizedResponse(c: Context, message: string): Response {
  return c.json(
    {
      success: false,
      statusCode: 401,
      messages: [message],
      inputErrors: [],
      data: null,
    },
    401,
  );
}
