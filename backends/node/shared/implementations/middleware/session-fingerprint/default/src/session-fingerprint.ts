import { createHash } from "node:crypto";
import { D2Result } from "@d2/result";

/** Callback to store a fingerprint for a session. */
export type StoreFingerprint = (sessionToken: string, fingerprint: string) => Promise<void>;
/** Callback to retrieve the stored fingerprint for a session. */
export type GetFingerprint = (sessionToken: string) => Promise<string | null>;
/** Callback to revoke a session (called on fingerprint mismatch). */
export type RevokeSession = (sessionToken: string) => Promise<void>;

export interface CheckSessionFingerprintOptions {
  /** Store a fingerprint for a session token. */
  storeFingerprint: StoreFingerprint;
  /** Retrieve the stored fingerprint for a session token. */
  getFingerprint: GetFingerprint;
  /** Revoke the session (called when fingerprint mismatch is detected). */
  revokeSession: RevokeSession;
  /** Optional logger for diagnostic output on catch-block failures. */
  logger?: { warn?: (msg: string, ...args: unknown[]) => void };
}

/**
 * Computes SHA-256 fingerprint from User-Agent and Accept headers.
 * Uses headers that identify the client software, which doesn't change mid-session.
 *
 * @param userAgent - User-Agent header value
 * @param accept - Accept header value
 * @returns 64-character lowercase hex string (SHA-256 hash)
 */
export function computeFingerprint(userAgent: string, accept: string): string {
  return createHash("sha256").update(`${userAgent}|${accept}`, "utf8").digest("hex");
}

/**
 * Checks a session fingerprint against the stored value.
 *
 * - First request (no stored fingerprint) -> stores the fingerprint, returns null
 * - Fingerprint matches -> null (proceed)
 * - Fingerprint mismatch -> revokes session, returns D2Result unauthorized
 *
 * @param sessionToken - The session token to check
 * @param fingerprint - The computed fingerprint for the current request
 * @param options - Callbacks for storage and revocation
 * @returns null if valid, D2Result error if fingerprint mismatch
 */
export async function checkSessionFingerprint(
  sessionToken: string,
  fingerprint: string,
  options: CheckSessionFingerprintOptions,
): Promise<D2Result | null> {
  let storedFingerprint: string | null;
  try {
    storedFingerprint = await options.getFingerprint(sessionToken);
  } catch (err: unknown) {
    // Redis down — fail open (same as rate limiter fail-open policy).
    options.logger?.warn?.(
      "Failed to get stored fingerprint, failing open",
      err instanceof Error ? err.message : String(err),
    );
    return null;
  }

  if (storedFingerprint === null) {
    // First request with this session token — store the fingerprint
    try {
      await options.storeFingerprint(sessionToken, fingerprint);
    } catch (err: unknown) {
      // Storage failure — continue without fingerprint binding
      options.logger?.warn?.(
        "Failed to store session fingerprint",
        err instanceof Error ? err.message : String(err),
      );
    }
    return null;
  }

  if (storedFingerprint !== fingerprint) {
    // Fingerprint mismatch — session token is likely stolen
    try {
      await options.revokeSession(sessionToken);
    } catch (err: unknown) {
      // Best-effort revocation — even if it fails, we reject this request
      options.logger?.warn?.(
        "Failed to revoke session after fingerprint mismatch",
        err instanceof Error ? err.message : String(err),
      );
    }
    return D2Result.unauthorized();
  }

  // Fingerprint matches — proceed
  return null;
}
