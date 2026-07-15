// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Server-side geo reference data accessor for the BFF layout.
 *
 * Returns null when Geo is unavailable so loaders can fall back to code-only
 * locale/timezone options (see `+layout.server.ts`). Full gRPC / cache wiring
 * is restored when the Geo client is re-attached to the web middleware stack;
 * until then a fail-soft null keeps production builds and layout loads healthy.
 */

import type { Country, Subdivision } from "@dcsv-io/d2-geo-abstractions";

/** Shape consumed by layout / account loaders (locales, timezones, countries). */
export type GeoRefDataPayload = {
  readonly locales?: Readonly<
    Record<
      string,
      {
        readonly ietfBcp47Tag: string;
        readonly name: string;
        readonly endonym: string;
        readonly languageIso6391Code: string;
        readonly countryIso31661Alpha2Code: string;
      }
    >
  >;
  readonly timezones?: Readonly<Record<string, unknown>>;
  readonly countries?: Readonly<Record<string, Country>>;
  readonly subdivisions?: Readonly<Record<string, Subdivision>>;
};

/**
 * Get geo reference data (memory-cached after first successful call in callers).
 * Currently fail-soft: returns null until middleware Geo client is wired.
 */
export async function getGeoRefData(): Promise<GeoRefDataPayload | null> {
  return null;
}
