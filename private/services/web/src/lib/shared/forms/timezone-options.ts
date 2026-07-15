// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Timezone option transforms for the timezone typeahead selector.
 *
 * Converts raw proto TimezoneDTO maps into searchable option arrays
 * with priority timezones pinned at the top.
 */
import type { TimezoneDTO } from "@dcsv-io/d2-protos";

/**
 * Priority timezones shown first in the typeahead (order preserved).
 * Covers all popular countries from the POPULAR_COUNTRIES list.
 */
const PRIORITY_TIMEZONES: readonly string[] = [
  // United States
  "America/New_York",
  "America/Chicago",
  "America/Denver",
  "America/Los_Angeles",
  "America/Anchorage",
  "Pacific/Honolulu",
  // Canada
  "America/Toronto",
  "America/Vancouver",
  "America/Edmonton",
  "America/Halifax",
  "America/St_Johns",
  "America/Winnipeg",
  // Mexico
  "America/Mexico_City",
  "America/Tijuana",
  // United Kingdom
  "Europe/London",
  // Germany
  "Europe/Berlin",
  // France
  "Europe/Paris",
  // Italy
  "Europe/Rome",
  // Spain
  "Europe/Madrid",
  // Ukraine
  "Europe/Kyiv",
  // Poland
  "Europe/Warsaw",
  // Netherlands
  "Europe/Brussels",
  // Australia
  "Australia/Sydney",
  "Australia/Perth",
  // New Zealand
  "Pacific/Auckland",
  // Japan
  "Asia/Tokyo",
  // South Korea
  "Asia/Seoul",
  // China
  "Asia/Shanghai",
  // Hong Kong
  "Asia/Hong_Kong",
  // Taiwan
  "Asia/Taipei",
  // Brazil
  "America/Sao_Paulo",
  // Colombia
  "America/Bogota",
  // Argentina
  "America/Argentina/Buenos_Aires",
  // Israel
  "Asia/Jerusalem",
  // Saudi Arabia
  "Asia/Riyadh",
  // United Arab Emirates
  "Asia/Dubai",
] as const;

export interface TimezoneOption {
  /** IANA timezone identifier (e.g., "America/New_York"). */
  value: string;
  /** Full display label with abbreviation (e.g., "America / New York (EST / EDT)"). */
  label: string;
  /** Human-readable name without abbreviation (e.g., "America / New York"). */
  displayName: string;
  /** Standard UTC offset (e.g., "-05:00"). */
  offset: string;
}

/**
 * Formats a timezone label from display name + abbreviations.
 * Examples:
 * - "America / New York" + EST + EDT → "America / New York (EST / EDT)"
 * - "Asia / Tokyo" + JST + null → "Asia / Tokyo (JST)"
 */
function formatLabel(displayName: string, abbrevSTD: string, abbrevDST?: string): string {
  const abbrev = abbrevDST ? `${abbrevSTD} / ${abbrevDST}` : abbrevSTD;
  return `${displayName} (${abbrev})`;
}

/**
 * Convert a timezones map to a sorted option array.
 * Priority timezones appear first (in PRIORITY_TIMEZONES order),
 * then remaining timezones sorted alphabetically by display name.
 */
export function timezonesToOptions(timezones: Record<string, TimezoneDTO>): TimezoneOption[] {
  // Drop entries missing required identity fields. Proto3 makes these
  // optional on the wire, but a TZ option with no IANA identifier or
  // display name has no UI value — we'd render an empty row.
  const all = Object.values(timezones).flatMap((t): TimezoneOption[] => {
    if (!t.ianaIdentifier || !t.displayName) return [];
    return [
      {
        value: t.ianaIdentifier,
        label: formatLabel(t.displayName, t.abbreviationStd ?? "", t.abbreviationDst || undefined),
        displayName: t.displayName,
        offset: t.utcOffsetStd ?? "",
      },
    ];
  });

  const prioritySet = new Set(PRIORITY_TIMEZONES);
  const byId = new Map(all.map((o) => [o.value, o]));

  const priority: TimezoneOption[] = [];
  for (const id of PRIORITY_TIMEZONES) {
    const opt = byId.get(id);
    if (opt) priority.push(opt);
  }

  const rest = all
    .filter((o) => !prioritySet.has(o.value))
    .sort((a, b) => a.displayName.localeCompare(b.displayName));

  return [...priority, ...rest];
}
