// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Mock middleware and auth context factories.
 *
 * Used when `D2_MOCK_INFRA=true` — provides stub handler implementations
 * so the full middleware pipeline runs with no-op behavior. Pages render
 * normally without real Redis, Geo gRPC, or Auth service connections.
 *
 * This file is only imported when the mock flag is set.
 */
import { TK } from "@d2/i18n/keys";
import { createLogger } from "@d2/logging";
import { D2Result } from "@d2/result";
import type { GeoRefData, CountryDTO, SubdivisionDTO } from "@d2/protos";
import type { SessionResolver, JwtManager, AuthProxy, AuthBffConfig } from "@d2/auth-bff-client";
import type { MiddlewareContext } from "./middleware.server.js";
import type { AuthContext } from "./auth.server.js";

// ---------------------------------------------------------------------------
// Stub handler factory
// ---------------------------------------------------------------------------

/** Creates a stub handler that returns ok() — satisfies any BaseHandler interface. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const stub = (): any => ({ handleAsync: async () => D2Result.ok() });

// ---------------------------------------------------------------------------
// Mock middleware context
// ---------------------------------------------------------------------------

export function createMockMiddlewareContext(): MiddlewareContext {
  const logger = createLogger({ serviceName: "d2-sveltekit" });

  return {
    logger,
    findWhoIs: stub(), // WhoIs skipped for localhost IPs anyway
    rateLimitCheck: stub(), // ok() → result.data?.isBlocked is falsy → passes
    idempotencyCheck: stub(),
    redisGet: stub(),
    redisSet: stub(),
    redisSetNx: stub(),
    redisRemove: stub(),
    getGeoRefData: {
      handleAsync: async () => D2Result.ok({ data: { data: MOCK_GEO_REF_DATA } }),
    } as unknown as MiddlewareContext["getGeoRefData"],
  };
}

// ---------------------------------------------------------------------------
// Mock auth context
// ---------------------------------------------------------------------------

export function createMockAuthContext(): AuthContext {
  const logger = createLogger({ serviceName: "d2-sveltekit-auth" });
  const config: AuthBffConfig = { authServiceUrl: "http://mock" };

  return {
    config,
    logger,
    sessionResolver: {
      resolve: async () => ({ session: null, user: null }),
    } as unknown as SessionResolver,
    jwtManager: {
      getToken: async () => null,
      invalidateToken: () => {},
    } as unknown as JwtManager,
    authProxy: {
      proxyRequest: async () =>
        new Response(
          JSON.stringify({ success: false, messages: [TK.common.errors.SERVICE_UNAVAILABLE] }),
          {
            status: 503,
            headers: { "Content-Type": "application/json" },
          },
        ),
    } as unknown as AuthProxy,
  };
}

// ---------------------------------------------------------------------------
// Mock geo reference data
// ---------------------------------------------------------------------------

function mockCountry(
  iso2: string,
  iso3: string,
  numeric: string,
  name: string,
  phonePrefix: string,
  phoneFormat: string,
  subdivisions: string[],
): CountryDTO {
  return {
    iso31661Alpha2Code: iso2,
    iso31661Alpha3Code: iso3,
    iso31661NumericCode: numeric,
    displayName: name,
    officialName: name,
    phoneNumberPrefix: phonePrefix,
    phoneNumberFormat: phoneFormat,
    sovereignIso31661Alpha2Code: iso2,
    primaryCurrencyIso4217AlphaCode: "",
    primaryLocaleIetfBcp47Tag: "",
    subdivisionIso31662Codes: subdivisions,
    territoryIso31661Alpha2Codes: [],
    localeIetfBcp47Tags: [],
    currencyIso4217AlphaCodes: [],
    geopoliticalEntityShortCodes: [],
  };
}

function mockSubdivision(iso2Code: string, shortCode: string, name: string): SubdivisionDTO {
  return {
    iso31662Code: `${iso2Code}-${shortCode}`,
    shortCode,
    displayName: name,
    officialName: name,
    countryIso31661Alpha2Code: iso2Code,
  };
}

const US_SUBDIVISIONS = [
  mockSubdivision("US", "CA", "California"),
  mockSubdivision("US", "NY", "New York"),
  mockSubdivision("US", "TX", "Texas"),
];

const CA_SUBDIVISIONS = [
  mockSubdivision("CA", "ON", "Ontario"),
  mockSubdivision("CA", "BC", "British Columbia"),
  mockSubdivision("CA", "QC", "Quebec"),
];

const MOCK_GEO_REF_DATA: GeoRefData = {
  version: "mock-1.0.0",
  updatedAt: new Date(),
  countries: {
    US: mockCountry(
      "US",
      "USA",
      "840",
      "United States",
      "1",
      "XXX-XXX-XXXX",
      US_SUBDIVISIONS.map((s) => s.iso31662Code).filter((c): c is string => !!c),
    ),
    CA: mockCountry(
      "CA",
      "CAN",
      "124",
      "Canada",
      "1",
      "XXX-XXX-XXXX",
      CA_SUBDIVISIONS.map((s) => s.iso31662Code).filter((c): c is string => !!c),
    ),
    SG: mockCountry("SG", "SGP", "702", "Singapore", "65", "XXXX XXXX", []),
    AF: mockCountry("AF", "AFG", "004", "Afghanistan", "93", "XXX XXX XXXX", []),
  },
  subdivisions: Object.fromEntries(
    [...US_SUBDIVISIONS, ...CA_SUBDIVISIONS].map((s) => [s.iso31662Code, s]),
  ),
  currencies: {},
  languages: {},
  locales: {},
  geopoliticalEntities: {},
};
