// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { XMLParser } from "fast-xml-parser";
import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "libphonenumber";
// upstream URL — cannot wrap
const SOURCE_URL =
  "https://raw.githubusercontent.com/google/libphonenumber/master/resources/PhoneNumberMetadata.xml";
const SOURCE_LICENSE = "Apache-2.0 (Google libphonenumber)";
const CACHE_KEY = "PhoneNumberMetadata.xml";

/**
 * Subset of the libphonenumber XML schema we consume. The full XML has ~30 fields
 * per territory; we extract only what's needed for Country.Phone* properties.
 *
 * Per-territory shape after parsing:
 *
 * <territory id="US" countryCode="1" internationalPrefix="011" nationalPrefix="1">
 *   <availableFormats>
 *     <numberFormat pattern="..." nationalPrefixOptionalWhenFormatting="true">
 *       <leadingDigits>...</leadingDigits>
 *       <format>($1) $2-$3</format>
 *     </numberFormat>
 *     ...
 *   </availableFormats>
 *   <generalDesc><nationalNumberPattern>...</nationalNumberPattern></generalDesc>
 *   <fixedLine><possibleLengths national="10" localOnly="7"/>...</fixedLine>
 *   <mobile><possibleLengths national="10"/>...</mobile>
 *   ...
 * </territory>
 */

export interface RawTerritoryAttributes {
  id: string;
  countryCode: string;
  internationalPrefix?: string;
  nationalPrefix?: string;
  mainCountryForCode?: string;
  mobileNumberPortableRegion?: string;
}

export interface RawNumberFormat {
  pattern: string;
  format: string;
  leadingDigits?: string;
  nationalPrefixFormattingRule?: string;
  nationalPrefixOptionalWhenFormatting?: string;
}

export interface RawPossibleLengths {
  national?: string;
  localOnly?: string;
}

export interface RawTerritory {
  attributes: RawTerritoryAttributes;
  formats: RawNumberFormat[];
  /**
   * National lengths from each per-type section (fixedLine, mobile, etc.) — used to
   * derive global min/max.
   */
  perTypePossibleLengths: RawPossibleLengths[];
}

export interface LibphonenumberFetchResult extends Pick<
  CachedFetch,
  "provenance" | "fromCache"
> {
  /**
   * Map keyed by territory id (ISO 3166-1 alpha-2 + a few non-ISO like "001" for
   * non-geographic numbers).
   */
  territoriesById: Map<string, RawTerritory>;
}

export async function fetchLibphonenumberMetadata(options?: {
  ttlHours?: number;
}): Promise<LibphonenumberFetchResult> {
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url: SOURCE_URL,
    license: SOURCE_LICENSE,
    cacheKey: CACHE_KEY,
    ttlHours: options?.ttlHours,
  });

  const xmlText = fetched.body.toString("utf8");
  const parser = new XMLParser({
    ignoreAttributes: false,
    attributeNamePrefix: "@_",
    parseAttributeValue: false,
    parseTagValue: false,
    trimValues: true,
    isArray: (name) => name === "territory" || name === "numberFormat",
  });
  const parsed = parser.parse(xmlText) as {
    phoneNumberMetadata?: {
      territories?: {
        territory?: Array<Record<string, unknown>>;
      };
    };
  };

  const territoriesArray = parsed.phoneNumberMetadata?.territories?.territory;
  if (!Array.isArray(territoriesArray)) {
    throw new Error(
      "libphonenumber XML structure unexpected — no territories array found",
    );
  }

  const territoriesById = new Map<string, RawTerritory>();
  for (const raw of territoriesArray) {
    const territory = normalizeTerritory(raw);
    if (territory) territoriesById.set(territory.attributes.id, territory);
  }

  return {
    territoriesById,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}

const TYPE_SECTIONS_WITH_LENGTHS = [
  "fixedLine",
  "mobile",
  "pager",
  "tollFree",
  "premiumRate",
  "sharedCost",
  "personalNumber",
  "voip",
  "uan",
  "voicemail",
  "noInternationalDialling",
];

function normalizeTerritory(raw: Record<string, unknown>): RawTerritory | null {
  const id = raw["@_id"];
  const countryCode = raw["@_countryCode"];
  if (typeof id !== "string" || typeof countryCode !== "string") return null;

  const attributes: RawTerritoryAttributes = {
    id,
    countryCode,
    internationalPrefix:
      typeof raw["@_internationalPrefix"] === "string"
        ? raw["@_internationalPrefix"]
        : undefined,
    nationalPrefix:
      typeof raw["@_nationalPrefix"] === "string"
        ? raw["@_nationalPrefix"]
        : undefined,
    mainCountryForCode:
      typeof raw["@_mainCountryForCode"] === "string"
        ? raw["@_mainCountryForCode"]
        : undefined,
    mobileNumberPortableRegion:
      typeof raw["@_mobileNumberPortableRegion"] === "string"
        ? raw["@_mobileNumberPortableRegion"]
        : undefined,
  };

  const formats: RawNumberFormat[] = [];
  const availableFormats = raw["availableFormats"];
  if (availableFormats && typeof availableFormats === "object") {
    const numberFormats = (availableFormats as { numberFormat?: unknown })
      .numberFormat;
    if (Array.isArray(numberFormats)) {
      for (const nf of numberFormats) {
        if (typeof nf !== "object" || nf === null) continue;
        const r = nf as Record<string, unknown>;
        const pattern = r["@_pattern"];
        const format = r["format"];
        if (typeof pattern !== "string" || typeof format !== "string") continue;
        const leadingDigits = pickString(r["leadingDigits"]);
        formats.push({
          pattern,
          format,
          leadingDigits,
          nationalPrefixFormattingRule:
            typeof r["@_nationalPrefixFormattingRule"] === "string"
              ? r["@_nationalPrefixFormattingRule"]
              : undefined,
          nationalPrefixOptionalWhenFormatting:
            typeof r["@_nationalPrefixOptionalWhenFormatting"] === "string"
              ? r["@_nationalPrefixOptionalWhenFormatting"]
              : undefined,
        });
      }
    }
  }

  const perTypePossibleLengths: RawPossibleLengths[] = [];
  for (const section of TYPE_SECTIONS_WITH_LENGTHS) {
    const block = raw[section];
    if (!block || typeof block !== "object") continue;
    const possibleLengths = (block as { possibleLengths?: unknown })
      .possibleLengths;
    if (!possibleLengths || typeof possibleLengths !== "object") continue;
    const pl = possibleLengths as Record<string, unknown>;
    perTypePossibleLengths.push({
      national:
        typeof pl["@_national"] === "string" ? pl["@_national"] : undefined,
      localOnly:
        typeof pl["@_localOnly"] === "string" ? pl["@_localOnly"] : undefined,
    });
  }

  return { attributes, formats, perTypePossibleLengths };
}

function pickString(value: unknown): string | undefined {
  if (typeof value === "string") return value.replace(/\s+/g, "").trim();
  return undefined;
}

if (
  process.argv[1]?.endsWith("libphonenumber-metadata.ts") ||
  process.argv[1]?.endsWith("libphonenumber-metadata.js")
) {
  const result = await fetchLibphonenumberMetadata();
  const us = result.territoriesById.get("US");
  const gb = result.territoriesById.get("GB");
  const jp = result.territoriesById.get("JP");
  console.log(
    JSON.stringify(
      {
        fromCache: result.fromCache,
        provenance: result.provenance,
        territoryCount: result.territoriesById.size,
        sampleUS: us
          ? {
              attributes: us.attributes,
              formatCount: us.formats.length,
              firstFormat: us.formats[0],
              perTypeLengths: us.perTypePossibleLengths,
            }
          : null,
        sampleGB: gb
          ? {
              attributes: gb.attributes,
              formatCount: gb.formats.length,
              perTypeLengths: gb.perTypePossibleLengths,
            }
          : null,
        sampleJP: jp
          ? {
              attributes: jp.attributes,
              formatCount: jp.formats.length,
              perTypeLengths: jp.perTypePossibleLengths,
            }
          : null,
      },
      null,
      2,
    ),
  );
}
