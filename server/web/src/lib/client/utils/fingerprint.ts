/**
 * Client-side browser fingerprint generator.
 *
 * Generates a SHA-256 hash from stable browser signals (screen, timezone, canvas, WebGL, etc.)
 * and stores it in the `d2-cfp` cookie for server-side consumption by request enrichment.
 *
 * The cookie is SameSite=Lax so it travels with same-origin navigations (SvelteKit SSR).
 * For cross-origin .NET gateway calls, the fingerprint is also set on the gateway client
 * as the `X-Client-Fingerprint` header.
 *
 * @module
 */

const COOKIE_NAME = "d2-cfp";
const COOKIE_MAX_AGE = 31_536_000; // 1 year in seconds

/** Cached fingerprint to avoid recomputation within the same page lifecycle. */
let cached: string | undefined;

/**
 * Read the `d2-cfp` cookie if it already exists.
 */
function readExistingCookie(): string | undefined {
  const match = document.cookie.split("; ").find((c) => c.startsWith(`${COOKIE_NAME}=`));
  return match?.split("=")[1];
}

/**
 * Write the fingerprint cookie.
 * Uses `Secure` flag when on HTTPS (production), omits it for local HTTP dev.
 */
function writeCookie(value: string): void {
  const secure = globalThis.location?.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${COOKIE_NAME}=${value}; path=/; max-age=${COOKIE_MAX_AGE}; SameSite=Lax${secure}`;
}

/**
 * Collect browser signals for fingerprinting.
 * Each signal is a string; missing/unsupported signals produce empty strings.
 */
function collectSignals(): string[] {
  const signals: string[] = [];

  // Screen geometry
  signals.push(`${screen.width}x${screen.height}`);
  signals.push(`${screen.colorDepth}`);
  signals.push(`${globalThis.devicePixelRatio ?? 1}`);

  // Timezone
  try {
    signals.push(Intl.DateTimeFormat().resolvedOptions().timeZone);
  } catch {
    signals.push("");
  }

  // Language
  signals.push(navigator.language);
  signals.push((navigator.languages ?? []).join(","));

  // Hardware
  signals.push(`${navigator.hardwareConcurrency ?? 0}`);
  signals.push(`${navigator.maxTouchPoints ?? 0}`);

  // Canvas fingerprint
  try {
    const canvas = document.createElement("canvas");
    canvas.width = 200;
    canvas.height = 50;
    const ctx = canvas.getContext("2d");
    if (ctx) {
      ctx.textBaseline = "top";
      ctx.font = "14px 'Arial'";
      ctx.fillStyle = "#f60";
      ctx.fillRect(125, 1, 62, 20);
      ctx.fillStyle = "#069";
      ctx.fillText("D2fp", 2, 15);
      ctx.fillStyle = "rgba(102, 204, 0, 0.7)";
      ctx.fillText("D2fp", 4, 17);
      signals.push(canvas.toDataURL());
    }
  } catch {
    // Canvas not available (e.g. worker context or privacy settings)
  }

  // WebGL renderer
  try {
    const canvas = document.createElement("canvas");
    const gl = canvas.getContext("webgl") ?? canvas.getContext("experimental-webgl");
    if (gl instanceof WebGLRenderingContext) {
      const debugInfo = gl.getExtension("WEBGL_debug_renderer_info");
      if (debugInfo) {
        signals.push(String(gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL) ?? ""));
        signals.push(String(gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL) ?? ""));
      }
    }
  } catch {
    // WebGL not available
  }

  return signals;
}

/**
 * Compute SHA-256 hex digest of a string using the Web Crypto API.
 */
async function sha256Hex(input: string): Promise<string> {
  const data = new TextEncoder().encode(input);
  const hashBuffer = await crypto.subtle.digest("SHA-256", data);
  const hashArray = new Uint8Array(hashBuffer);
  return Array.from(hashArray, (b) => b.toString(16).padStart(2, "0")).join("");
}

/**
 * Generate a client fingerprint from browser signals.
 *
 * - If the `d2-cfp` cookie already exists, returns its value (stable across page loads).
 * - Otherwise, collects browser signals, hashes them, writes the cookie, and returns the hash.
 *
 * The returned value should be passed to `setClientFingerprint()` on the gateway client
 * so cross-origin API calls include the `X-Client-Fingerprint` header.
 *
 * @returns SHA-256 hex string (64 characters)
 */
export async function generateClientFingerprint(): Promise<string> {
  // Return cached value if we already computed this session
  if (cached) return cached;

  // Check if cookie already exists from a previous page load
  const existing = readExistingCookie();
  if (existing && existing.length === 64) {
    cached = existing;
    return cached;
  }

  // Compute fresh fingerprint
  const signals = collectSignals();
  const combined = signals.join("|");
  const hash = await sha256Hex(combined);

  // Persist
  writeCookie(hash);
  cached = hash;
  return hash;
}
