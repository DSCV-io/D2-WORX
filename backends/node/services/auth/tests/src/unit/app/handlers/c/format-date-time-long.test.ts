import { describe, it, expect } from "vitest";
import { formatDateTimeLong } from "@d2/auth-app";

/**
 * Regression tests for the deletion-email date formatter.
 *
 * Initial implementation used `dateStyle: "long" + timeStyle: "short" +
 * timeZoneName: "short"`. The first two are spec-incompatible with
 * `timeZoneName` — Intl.DateTimeFormat throws RangeError, the catch
 * block silently rendered without a timezone, and recipients got UTC
 * times labeled with no abbreviation. Anchoring the field-options
 * approach here so that regression can't recur silently.
 */
describe("formatDateTimeLong (request-user-deletion email helper)", () => {
  // Fixed reference instant — 2026-05-22 18:35:00 UTC. With America/Edmonton
  // (UTC-6 in May, MDT), this is 12:35 PM MDT.
  const date = new Date("2026-05-22T18:35:00.000Z");

  it("renders the timezone abbreviation when a valid IANA zone is given", () => {
    const out = formatDateTimeLong(date, "en-US", "America/Edmonton");
    // We don't pin exact format strings (Intl varies between Node/ICU
    // versions) — just assert the meaningful fragments are present:
    expect(out).toMatch(/2026/);
    expect(out).toMatch(/May/);
    expect(out).toMatch(/22/);
    // The bug was that this abbreviation got dropped entirely. The
    // exact short-form ("MDT" / "GMT-6") varies by ICU version, so just
    // assert _some_ tz indicator is present after the time digits.
    expect(out).toMatch(/(MDT|MST|GMT[+-−]\d|UTC[+-−]?\d?)/);
  });

  it("expresses the date in the supplied timezone, not UTC", () => {
    // 18:35 UTC → 12:35 MDT. The hour shown must be 12 (not 18 / 6).
    const out = formatDateTimeLong(date, "en-US", "America/Edmonton");
    expect(out).toMatch(/12:35/);
    expect(out).not.toMatch(/18:35/);
    expect(out).not.toMatch(/6:35/);
  });

  it("falls back to UTC when the supplied timezone is invalid", () => {
    // Bogus IANA → Intl.DateTimeFormat throws on construct → catch block
    // renders against UTC so the email still goes out instead of swallowing
    // the deletion confirmation entirely.
    const out = formatDateTimeLong(date, "en-US", "Not/A_Real_Tz");
    expect(out).toMatch(/2026/);
    // en-US uses 12-hour format → 18:35 UTC renders as "6:35 PM". Critically
    // it must NOT render as 12:35 (Edmonton-shifted), which would mean the
    // catch path silently honored the bad input.
    expect(out).toMatch(/6:35/);
    expect(out).not.toMatch(/12:35/);
  });

  it("respects the locale for ordering and naming", () => {
    // French Canadian date format — verifies the locale arg threads through.
    // Just asserting the FRENCH month name is present (mai = May).
    const out = formatDateTimeLong(date, "fr-CA", "America/Edmonton");
    expect(out).toMatch(/mai/);
  });

  it("includes the year, month, day, hour, and minute (the field options)", () => {
    // Anchoring that we use field options rather than dateStyle/timeStyle.
    // Each of these would be missing if we ever revert to dateStyle alone.
    const out = formatDateTimeLong(date, "en-US", "UTC");
    expect(out).toMatch(/2026/);
    expect(out).toMatch(/22/);
    expect(out).toMatch(/35/); // minute
  });
});
