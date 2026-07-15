// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { UAParser } from "ua-parser-js";

/**
 * Parsed user-agent — friendly labels for display in the security tab.
 *
 * `browser` and `os` collapse to "Unknown" rather than empty strings so
 * downstream UI never has to special-case missing data.
 */
export interface ParsedUserAgent {
  /** Short browser name for the row chip (e.g. "Chrome"). */
  browser: string;
  /** Short OS name for the row chip (e.g. "Windows"). */
  os: string;
  /** Long browser name with major version for the hover tooltip (e.g. "Chrome 147"). */
  browserLong: string;
  /** Long OS name with version for the hover tooltip (e.g. "iOS 18.7"). */
  osLong: string;
  /** "desktop" | "mobile" | "tablet" | "console" | "smarttv" | "wearable" | "embedded" | "unknown" */
  deviceType: string;
}

/**
 * Parse a User-Agent string into friendly labels. Safe to call with undefined
 * or empty input — returns an "Unknown" placeholder so the UI never has to
 * branch on missing data.
 */
export function parseUserAgent(ua: string | undefined | null): ParsedUserAgent {
  if (!ua) {
    return {
      browser: "Unknown",
      os: "Unknown",
      browserLong: "Unknown",
      osLong: "Unknown",
      deviceType: "unknown",
    };
  }
  const parsed = new UAParser(ua).getResult();
  // Short forms are rendered in the row chip; long forms (with version) live
  // in the hover tooltip. The chip stays compact while versions remain
  // available for users who want to verify forensic detail.
  const browser = parsed.browser.name ?? "Unknown";
  const os = parsed.os.name ?? "Unknown";
  const browserLong = parsed.browser.version
    ? `${browser} ${parsed.browser.version.split(".")[0]}`
    : browser;
  // Windows on Chrome/Edge always reports `Windows NT 10.0` regardless of
  // 10 vs 11 (Microsoft froze the UA major version for compat). Skip the
  // version on Windows until we wire up UA Client Hints (Sec-CH-UA-Platform-Version).
  const osLong = os === "Windows" || !parsed.os.version ? os : `${os} ${parsed.os.version}`;
  const deviceType = parsed.device.type ?? "desktop";
  return { browser, os, browserLong, osLong, deviceType };
}
