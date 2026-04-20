import { UAParser } from "ua-parser-js";

/**
 * Parsed user-agent — friendly labels for display in the security tab.
 *
 * `browser` and `os` collapse to "Unknown" rather than empty strings so
 * downstream UI never has to special-case missing data.
 */
export interface ParsedUserAgent {
  browser: string;
  os: string;
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
    return { browser: "Unknown", os: "Unknown", deviceType: "unknown" };
  }
  const parsed = new UAParser(ua).getResult();
  const browser = parsed.browser.name
    ? parsed.browser.version
      ? `${parsed.browser.name} ${parsed.browser.version.split(".")[0]}`
      : parsed.browser.name
    : "Unknown";
  // Windows on Chrome/Edge always reports `Windows NT 10.0` regardless of
  // 10 vs 11 (Microsoft froze the UA major version for compat). Showing "10"
  // would be wrong half the time, so collapse to just "Windows" until we
  // wire up UA Client Hints (Sec-CH-UA-Platform-Version).
  const osName = parsed.os.name ?? "";
  const os = osName
    ? osName === "Windows"
      ? "Windows"
      : parsed.os.version
        ? `${osName} ${parsed.os.version}`
        : osName
    : "Unknown";
  const deviceType = parsed.device.type ?? "desktop";
  return { browser, os, deviceType };
}
