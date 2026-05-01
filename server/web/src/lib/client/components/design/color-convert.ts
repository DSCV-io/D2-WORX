/**
 * OKLCH <-> hex color conversion utilities using culori.
 *
 * Imports from "culori/fn" for tree-shaking (~5-8KB vs ~30KB full).
 */

import { useMode, modeOklch, modeRgb, formatHex, parse } from "culori/fn";
import type { OklchColor } from "./theme-utils.js";

// Register both modes — parse returns RGB, oklch converter needs it registered
useMode(modeRgb);
const oklch = useMode(modeOklch);

/**
 * Convert OKLCH values to a hex color string.
 *
 * Out-of-gamut OKLCH values are clamped to sRGB by culori's formatHex.
 */
export function oklchToHex(l: number, c: number, h: number): string {
  const color = { mode: "oklch" as const, l, c, h };
  return formatHex(color);
}

/**
 * Convert a hex color string to OKLCH values.
 *
 * Returns null for invalid input. Achromatic colors (chroma ~0) may
 * have undefined hue in culori — defaults to 0.
 */
export function hexToOklch(hex: string): OklchColor | null {
  const parsed = parse(hex);
  if (!parsed) return null;

  const result = oklch(parsed);
  if (!result) return null;

  return {
    lightness: result.l,
    chroma: result.c,
    hue: result.h ?? 0,
  };
}
