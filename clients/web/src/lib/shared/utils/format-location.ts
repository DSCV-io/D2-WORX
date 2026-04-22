import type { WhoIsLite } from "$lib/client/rest/account-client.js";

/**
 * Short location string for the row chip: `City, ST` (no country code, since
 * the country is already conveyed by the flag rendered next to it).
 * Returns an empty string when no location data is available.
 */
export function formatLocation(whoIs: WhoIsLite | undefined): string {
  if (!whoIs?.location) return "";
  const { city, subdivisionIso31662Code } = whoIs.location;
  const parts: string[] = [];
  if (city) parts.push(city);
  // Subdivision codes are formatted "US-CA" in proto — strip the country prefix
  // for display when the country is also present.
  if (subdivisionIso31662Code) {
    const [, sub] = subdivisionIso31662Code.split("-");
    parts.push(sub ?? subdivisionIso31662Code);
  }
  return parts.join(", ");
}

/**
 * Long location string for the chip's hover tooltip: `City, Subdivision, Country`.
 * Includes the country code (the flag conveys it visually but the tooltip is
 * the place to surface the full text).
 */
export function formatLocationLong(whoIs: WhoIsLite | undefined): string {
  if (!whoIs?.location) return "";
  const { city, subdivisionIso31662Code, countryIso31661Alpha2Code } = whoIs.location;
  const parts: string[] = [];
  if (city) parts.push(city);
  if (subdivisionIso31662Code) {
    const [, sub] = subdivisionIso31662Code.split("-");
    parts.push(sub ?? subdivisionIso31662Code);
  }
  if (countryIso31661Alpha2Code) parts.push(countryIso31661Alpha2Code);
  return parts.join(", ");
}

/**
 * Returns the ISO 3166-1 alpha-2 country code from a WhoIs payload, or
 * undefined if missing. Used as the source for `<CountryFlag>` next to
 * the formatted location string.
 */
export function locationCountryCode(whoIs: WhoIsLite | undefined): string | undefined {
  return whoIs?.location?.countryIso31661Alpha2Code ?? undefined;
}
