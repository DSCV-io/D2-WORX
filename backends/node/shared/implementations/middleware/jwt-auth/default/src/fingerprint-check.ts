import { subtle, timingSafeEqual } from "node:crypto";
import type { JWTPayload } from "jose";
import { D2Result } from "@d2/result";

/**
 * Computes the server-side fingerprint hash: SHA-256(userAgent + "|" + accept).
 * Matches the algorithm used by @d2/request-enrichment when the JWT was issued.
 */
async function computeFingerprint(userAgent: string, accept: string): Promise<string> {
  const data = new TextEncoder().encode(`${userAgent}|${accept}`);
  const hash = await subtle.digest("SHA-256", data);
  return Array.from(new Uint8Array(hash))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

/**
 * Validates the fingerprint claim in a JWT against the current request headers.
 *
 * If the JWT contains an `fp` claim, it must match SHA-256(UA|Accept) of the
 * current request. A mismatch indicates the token may have been stolen (replayed
 * from a different client).
 *
 * **Fail-closed by default:** when `fp` is absent the request is rejected. The
 * old "backward compat" soft-pass let any future issuer/dev token bypass
 * fingerprint binding entirely. Set `allowMissingClaim: true` to opt back into
 * the legacy posture for development paths that explicitly mint tokens without
 * `fp` (none in production).
 *
 * @returns `ok()` on match (or on absent claim when `allowMissingClaim` is
 *   true), `unauthorized()` otherwise.
 */
export async function checkFingerprint(
  payload: JWTPayload,
  userAgent: string,
  accept: string,
  options: { allowMissingClaim?: boolean } = {},
): Promise<D2Result<void>> {
  const expectedFingerprint = payload["fp"];
  if (typeof expectedFingerprint !== "string") {
    if (options.allowMissingClaim === true) return D2Result.ok();
    return D2Result.unauthorized({
      messages: ["Fingerprint claim required."],
    });
  }

  const actual = await computeFingerprint(userAgent, accept);
  const a = Buffer.from(actual, "utf-8");
  const b = Buffer.from(expectedFingerprint, "utf-8");
  if (a.length !== b.length || !timingSafeEqual(a, b)) {
    return D2Result.unauthorized({ messages: ["Fingerprint mismatch."] });
  }

  return D2Result.ok();
}
