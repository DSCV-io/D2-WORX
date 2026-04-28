import type { ILogger } from "@d2/logging";
import type { IRequestContext } from "@d2/handler";
import type { FindWhoIs } from "@d2/geo-client";
import { resolveIp, isLocalhost } from "./ip-resolver.js";
import { buildServerFingerprint, buildDeviceFingerprint } from "./fingerprint-builder.js";
import { MutableRequestContext } from "./request-info.js";
import {
  DEFAULT_REQUEST_ENRICHMENT_OPTIONS,
  type RequestEnrichmentOptions,
} from "./request-enrichment-options.js";

/**
 * Enriches an HTTP request with client information.
 * Framework-agnostic — takes a plain headers object.
 * Mirrors the logic of D2.Shared.RequestEnrichment.Default.RequestEnrichmentMiddleware in .NET.
 *
 * Always succeeds — never throws. Fail-open on WhoIs errors.
 *
 * @param headers - Plain headers object (lowercase keys).
 * @param findWhoIs - The FindWhoIs handler from @d2/geo-client.
 * @param options - Optional partial options (merged with defaults).
 * @param logger - Optional logger for diagnostic output.
 */
export async function enrichRequest(
  headers: Record<string, string | string[] | undefined>,
  findWhoIs: FindWhoIs,
  options?: Partial<RequestEnrichmentOptions>,
  logger?: ILogger,
): Promise<IRequestContext> {
  const opts = { ...DEFAULT_REQUEST_ENRICHMENT_OPTIONS, ...options };

  // 1. Resolve client IP (only trusting configured proxy headers).
  const clientIp = resolveIp(headers, opts.trustedProxyHeaders, opts.maxForwardedForLength);

  // 2. Compute server fingerprint.
  const serverFingerprint = buildServerFingerprint(headers);

  // 3. Read client fingerprint: cookie (primary) → header (fallback).
  let clientFingerprint = parseCookieValue(headers, opts.clientFingerprintCookie);
  if (!clientFingerprint) {
    const clientFingerprintRaw = headers[opts.clientFingerprintHeader];
    clientFingerprint = clientFingerprintRaw
      ? Array.isArray(clientFingerprintRaw)
        ? clientFingerprintRaw[0]
        : clientFingerprintRaw
      : undefined;
  }
  if (clientFingerprint && clientFingerprint.length > opts.maxFingerprintLength) {
    clientFingerprint = clientFingerprint.slice(0, opts.maxFingerprintLength);
  }

  if (!clientFingerprint) {
    logger?.warn(
      "Client fingerprint missing (no d2-cfp cookie or X-Client-Fingerprint header). Device rate-limit bucket will be shared.",
    );
  }

  // 4. Compute combined device fingerprint (always present).
  const deviceFingerprint = buildDeviceFingerprint(clientFingerprint, serverFingerprint, clientIp);

  // 4. Build initial request context (without WhoIs data).
  let requestContext: IRequestContext = new MutableRequestContext({
    clientIp,
    serverFingerprint,
    clientFingerprint,
    deviceFingerprint,
  });

  // 5. Perform WhoIs lookup if enabled and not localhost.
  if (opts.enableWhoIsLookup && !isLocalhost(clientIp)) {
    try {
      const whoIsResult = await findWhoIs.handleAsync({
        ipAddress: clientIp,
      });

      const output = whoIsResult.checkSuccess();
      if (output?.whoIs) {
        const whoIs = output.whoIs;
        requestContext = new MutableRequestContext({
          clientIp,
          serverFingerprint,
          clientFingerprint,
          deviceFingerprint,
          whoIsHashId: whoIs.hashId || undefined,
          city: whoIs.location?.city || undefined,
          countryCode: whoIs.location?.countryIso31661Alpha2Code || undefined,
          subdivisionCode: whoIs.location?.subdivisionIso31662Code || undefined,
          isVpn: whoIs.isVpn,
          isProxy: whoIs.isProxy,
          isTor: whoIs.isTor,
          isHosting: whoIs.isHosting,
        });

        logger?.debug(
          `Enriched request with WhoIs data. City: ${whoIs.location?.city}, Country: ${whoIs.location?.countryIso31661Alpha2Code}`,
        );
      } else {
        logger?.debug("WhoIs lookup returned no data for client");
      }
    } catch (err: unknown) {
      // Fail-open: log warning and continue without WhoIs data.
      logger?.warn(
        `WhoIs lookup failed. Proceeding without WhoIs data: ${err instanceof Error ? err.message : String(err)}`,
      );
    }
  }

  return requestContext;
}

/**
 * Parses a cookie value from the raw `cookie` header.
 * Returns undefined if the cookie is not found.
 */
function parseCookieValue(
  headers: Record<string, string | string[] | undefined>,
  cookieName: string,
): string | undefined {
  const cookieHeader = headers["cookie"];
  if (!cookieHeader) return undefined;
  const raw = Array.isArray(cookieHeader) ? cookieHeader[0] : cookieHeader;
  if (!raw) return undefined;

  for (const pair of raw.split(";")) {
    const eqIndex = pair.indexOf("=");
    if (eqIndex === -1) continue;
    const name = pair.slice(0, eqIndex).trim();
    if (name === cookieName) {
      return pair.slice(eqIndex + 1).trim() || undefined;
    }
  }
  return undefined;
}
