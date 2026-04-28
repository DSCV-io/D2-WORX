import { createRemoteJWKSet } from "jose";
import type { JWTVerifyGetKey } from "jose";

/**
 * Creates a cached JWKS provider that fetches keys from a remote JWKS endpoint.
 * The provider caches keys in memory and refreshes automatically when needed.
 *
 * @param jwksUrl - URL of the JWKS endpoint (e.g., "http://d2-auth:5100/api/auth/jwks")
 */
export function createJwksProvider(jwksUrl: string): JWTVerifyGetKey {
  return createRemoteJWKSet(new URL(jwksUrl));
}
