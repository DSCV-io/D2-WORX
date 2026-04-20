import type { WhoIsLite } from "$lib/client/rest/account-client.js";

/**
 * Best-effort friendly location string from a WhoIs payload — `City, ST` or
 * `City, US` etc. Returns an empty string when no location data is available
 * (callers should fall back to the raw IP).
 *
 * v1: uses raw ISO codes for state/country. A country-name lookup via the
 * preloaded Geo ref data is a nice follow-up, but the codes are already
 * recognizable for the common cases this surfaces (security tab review).
 */
export function formatLocation(whoIs: WhoIsLite | undefined): string {
  if (!whoIs?.location) return "";
  const { city, subdivisionIso31662Code, countryIso31661Alpha2Code } = whoIs.location;
  const parts: string[] = [];
  if (city) parts.push(city);
  // Subdivision codes are formatted "US-CA" in proto — strip the country prefix
  // for display when the country is also present.
  if (subdivisionIso31662Code) {
    const [, sub] = subdivisionIso31662Code.split("-");
    parts.push(sub ?? subdivisionIso31662Code);
  }
  if (countryIso31661Alpha2Code) parts.push(countryIso31661Alpha2Code);
  return parts.join(", ");
}
