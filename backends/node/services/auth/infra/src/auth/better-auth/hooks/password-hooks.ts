import { createHash } from "node:crypto";
import { APIError } from "better-auth";
import { hashPassword, verifyPassword } from "better-auth/crypto";
import { validatePassword, PASSWORD_POLICY } from "@d2/auth-domain";
import { TK } from "@d2/i18n";

/** Minimal cache interface — satisfied by MemoryCacheStore. */
export interface PrefixCache {
  get<T>(key: string): T | undefined;
  set<T>(key: string, value: T, expirationMs?: number): void;
}

export interface BreachCheckResult {
  breached: boolean;
  count?: number;
}

export interface PasswordFunctions {
  hash: (password: string) => Promise<string>;
  verify: (data: { hash: string; password: string }) => Promise<boolean>;
}

export interface Logger {
  warn: (message: string, ...args: unknown[]) => void;
}

/**
 * Checks a password against the HaveIBeenPwned k-anonymity API.
 *
 * Uses SHA-1 prefix (5 chars) for k-anonymity — the full hash is never sent.
 * Results are cached by prefix (24h TTL) to reduce API calls.
 *
 * Fail-open: if the HIBP API is unreachable, returns `{ breached: false }`
 * and logs a warning. The local common-password blocklist still protects
 * against the most trivially guessable passwords.
 */
export async function checkBreachedPassword(
  password: string,
  cache: PrefixCache,
  logger?: Logger,
): Promise<BreachCheckResult> {
  const sha1 = createHash("sha1").update(password).digest("hex").toUpperCase();
  const prefix = sha1.slice(0, 5);
  const suffix = sha1.slice(5);

  // Check cache first (keyed by 5-char prefix)
  let responseText = cache.get<string>(prefix);

  if (responseText === undefined) {
    try {
      const response = await fetch(`${PASSWORD_POLICY.HIBP_API_BASE}${prefix}`, {
        headers: { "User-Agent": "D2-WORX-Auth" },
      });

      if (!response.ok) {
        logger?.warn(`HIBP API returned ${response.status} for prefix ${prefix} — failing open`);
        return { breached: false };
      }

      responseText = await response.text();
      cache.set(prefix, responseText, PASSWORD_POLICY.HIBP_CACHE_TTL_MS);
    } catch (error) {
      logger?.warn(
        "HIBP API unreachable — failing open",
        error instanceof Error ? error.message : String(error),
      );
      return { breached: false };
    }
  }

  // HIBP response format: newline-separated "SUFFIX:COUNT" lines
  for (const line of responseText.split("\n")) {
    const parts = line.split(":");
    const lineSuffix = parts[0];
    const countStr = parts[1];
    if (lineSuffix && lineSuffix.trim().toUpperCase() === suffix) {
      return { breached: true, count: parseInt(countStr?.trim() ?? "0", 10) };
    }
  }

  return { breached: false };
}

/**
 * Creates the `{ hash, verify }` object for BetterAuth's
 * `emailAndPassword.password` config.
 *
 * - `hash` runs domain validation (sync) + HIBP check (async, fail-open),
 *   then delegates to BetterAuth's own bcrypt hashing.
 * - `verify` is a pass-through to BetterAuth's bcrypt verifier — no
 *   custom logic on login (we only validate on creation / reset).
 */
export function createPasswordFunctions(cache: PrefixCache, logger?: Logger): PasswordFunctions {
  return {
    hash: async (password: string): Promise<string> => {
      // 1. Domain validation (sync — numeric-only, date-like, common blocklist)
      const result = validatePassword(password);
      if (!result.valid) {
        throw new APIError("BAD_REQUEST", { message: result.message });
      }

      // 2. HIBP check (async, fail-open)
      const hibp = await checkBreachedPassword(password, cache, logger);
      if (hibp.breached) {
        throw new APIError("BAD_REQUEST", {
          message: TK.auth.errors.PASSWORD_BREACHED,
        });
      }

      // 3. Hash with BetterAuth's own bcrypt
      return hashPassword(password);
    },

    verify: verifyPassword,
  };
}
