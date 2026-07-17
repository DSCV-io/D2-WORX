// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Pure functions for deriving OKLCH theme tokens from primary color values.
 * No runes — this module is plain TypeScript.
 *
 * Token keys use intermediate variable names (--background, --foreground, etc.)
 * matching the :root/.dark blocks in app.css. The @theme inline block maps
 * --color-* to var(--*) so Tailwind utilities preserve var() references.
 */

export interface OklchColor {
  hue: number;
  chroma: number;
  lightness: number;
}

export interface ThemeConfig {
  primary: OklchColor;
  secondary: OklchColor;
  accent: OklchColor;
  destructive: OklchColor;
  info: OklchColor;
  success: OklchColor;
  warning: OklchColor;
  radius: number;
}

export type ThemeTokens = Record<string, string>;

function oklch(l: number, c: number, h: number): string {
  return `oklch(${round(l)} ${round(c)} ${round(h)})`;
}

function oklchAlpha(l: number, c: number, h: number, alpha: string): string {
  return `oklch(${round(l)} ${round(c)} ${round(h)} / ${alpha})`;
}

function round(n: number): string {
  return Number(n.toFixed(4)).toString();
}

/** Distribute chart hues evenly around the color wheel from a starting hue. */
function chartHues(baseHue: number): number[] {
  return [0, 60, 150, 210, 300].map((offset) => (baseHue + offset) % 360);
}

/**
 * Compute a light-on-dark or dark-on-light foreground for a given background.
 * If the background lightness is >= 0.7, use dark foreground; else light.
 */
function autoForeground(bgLightness: number): string {
  return bgLightness >= 0.7 ? oklch(0.21, 0.006, 285.885) : oklch(0.985, 0, 0);
}

/** All semantic token names (without -- prefix). */
export const SEMANTIC_TOKENS = [
  "background",
  "foreground",
  "card",
  "card-foreground",
  "popover",
  "popover-foreground",
  "primary",
  "primary-foreground",
  "secondary",
  "secondary-foreground",
  "muted",
  "muted-foreground",
  "accent",
  "accent-foreground",
  "destructive",
  "destructive-foreground",
  "info",
  "info-foreground",
  "success",
  "success-foreground",
  "warning",
  "warning-foreground",
  "border",
  "input",
  "ring",
] as const;

export const CHART_TOKENS = ["chart-1", "chart-2", "chart-3", "chart-4", "chart-5"] as const;

export const SIDEBAR_TOKENS = [
  "sidebar",
  "sidebar-foreground",
  "sidebar-primary",
  "sidebar-primary-foreground",
  "sidebar-accent",
  "sidebar-accent-foreground",
  "sidebar-border",
  "sidebar-ring",
] as const;

export function computeLightTokens(config: ThemeConfig): ThemeTokens {
  const { hue: h, chroma: c, lightness: l } = config.primary;
  const sec = config.secondary;
  const acc = config.accent;
  const d = config.destructive;
  const info = config.info;
  const success = config.success;
  const warning = config.warning;
  const charts = chartHues(h);

  // Layered surface strategy (mirrors Stripe / Linear / Vercel):
  //   page bg (off-white) → card (white, lifts above page) → input (white, with stronger border)
  // One step of contrast between each plane. Foreground is near-black with
  // a tiny amount of brand-hue chroma for harmony — NOT brand-colored.
  return {
    "--background": oklch(0.985, 0.002, h), // off-white page
    "--foreground": oklch(0.18, 0.005, h), // near-black, brand-tinted (almost imperceptibly)
    "--card": oklch(1, 0, 0), // pure white surface — lifts above page
    "--card-foreground": oklch(0.18, 0.005, h),
    "--popover": oklch(1, 0, 0),
    "--popover-foreground": oklch(0.18, 0.005, h),
    "--primary": oklch(l, c, h),
    "--primary-foreground": oklch(0.985, 0, 0),
    "--secondary": oklch(sec.lightness, sec.chroma, sec.hue),
    "--secondary-foreground": autoForeground(sec.lightness),
    "--muted": oklch(0.96, Math.min(sec.chroma * 0.15, 0.012), sec.hue),
    "--muted-foreground": oklch(0.48, Math.min(sec.chroma * 1.0, 0.025), sec.hue),
    "--accent": oklch(acc.lightness, acc.chroma, acc.hue),
    "--accent-foreground": oklch(0.985, 0, 0),
    "--destructive": oklch(d.lightness, d.chroma, d.hue),
    "--destructive-foreground": oklch(0.985, 0, 0),
    "--info": oklch(info.lightness, info.chroma, info.hue),
    "--info-foreground": oklch(0.985, 0, 0),
    "--success": oklch(success.lightness, success.chroma, success.hue),
    "--success-foreground": oklch(0.985, 0, 0),
    "--warning": oklch(warning.lightness, warning.chroma, warning.hue),
    "--warning-foreground": oklch(0.985, 0, 0),
    // Border: visible enough to outline fields without being loud.
    "--border": oklch(0.88, 0.008, h),
    // Input bg is distinctly darker than BOTH page bg (white) and card bg
    // (white) so form fields are recognizable wherever they sit. Tuned to
    // read as a clearly different surface without feeling heavy.
    "--input": oklch(0.955, 0.006, h),
    "--ring": oklch(l, c, h), // focus ring = brand color for clear interaction signal
    "--chart-1": oklch(0.646, 0.222, charts[0]),
    "--chart-2": oklch(0.6, 0.118, charts[1]),
    "--chart-3": oklch(0.398, 0.07, charts[2]),
    "--chart-4": oklch(0.828, 0.189, charts[3]),
    "--chart-5": oklch(0.769, 0.188, charts[4]),
    "--sidebar": oklch(0.985, 0.002, h),
    "--sidebar-foreground": oklch(0.18, 0.005, h),
    "--sidebar-primary": oklch(l, c, h),
    "--sidebar-primary-foreground": oklch(0.985, 0, 0),
    "--sidebar-accent": oklch(0.94, Math.min(acc.chroma * 0.15, 0.025), acc.hue),
    "--sidebar-accent-foreground": oklch(0.18, 0.005, h),
    "--sidebar-border": oklch(0.9, 0.005, h),
    "--sidebar-ring": oklch(l, c, h),
  };
}

export function computeDarkTokens(config: ThemeConfig): ThemeTokens {
  // Lightness intentionally discarded — dark mode uses a fixed near-white
  // primary tint instead of the user's brand lightness (see comment below).
  const { hue: h, chroma: c, lightness: _l } = config.primary;
  const sec = config.secondary;
  const acc = config.accent;
  const d = config.destructive;
  const info = config.info;
  const success = config.success;
  const warning = config.warning;
  const charts = chartHues(h);

  // Dark-mode primary stays close to the original near-white tint —
  // gives a clean monochrome look on dark bg, which the user prefers
  // over a saturated brand teal. (Brand identity still surfaces via
  // the logo tile + accent color elsewhere.)
  const darkPrimaryL = 0.92;
  const darkPrimaryC = Math.min(c * 0.17, 0.02);

  // Boost lightness for status colors so they're visible on dark backgrounds.
  const darkDestructiveL = Math.min(d.lightness + 0.13, 0.85);
  const darkInfoL = Math.min(info.lightness + 0.1, 0.85);
  const darkSuccessL = Math.min(success.lightness + 0.09, 0.85);
  const darkWarningL = Math.min(warning.lightness + 0.05, 0.9);

  return {
    // Layered surfaces: page → card (lifted) → input (further lifted, solid).
    "--background": oklch(0.17, Math.min(c * 0.83, 0.015), h),
    "--foreground": oklch(0.985, 0, 0),
    "--card": oklch(0.21, Math.min(c * 0.5, 0.025), h),
    "--card-foreground": oklch(0.985, 0, 0),
    "--popover": oklch(0.21, Math.min(c * 0.5, 0.025), h),
    "--popover-foreground": oklch(0.985, 0, 0),
    "--primary": oklch(darkPrimaryL, darkPrimaryC, h),
    "--primary-foreground": oklch(0.21, Math.min(c * 0.5, 0.025), h),
    "--secondary": oklch(0.274, Math.min(sec.chroma * 0.4, 0.04), sec.hue),
    "--secondary-foreground": oklch(0.985, 0, 0),
    "--muted": oklch(0.274, Math.min(sec.chroma * 0.3, 0.02), sec.hue),
    "--muted-foreground": oklch(0.705, Math.min(sec.chroma * 1.5, 0.05), sec.hue),
    "--accent": oklch(acc.lightness, acc.chroma, acc.hue),
    "--accent-foreground": oklch(0.985, 0, 0),
    "--destructive": oklch(darkDestructiveL, d.chroma * 0.78, d.hue),
    "--destructive-foreground": autoForeground(darkDestructiveL),
    "--info": oklch(darkInfoL, info.chroma * 0.9, info.hue),
    "--info-foreground": autoForeground(darkInfoL),
    "--success": oklch(darkSuccessL, success.chroma * 0.9, success.hue),
    "--success-foreground": autoForeground(darkSuccessL),
    "--warning": oklch(darkWarningL, warning.chroma * 0.9, warning.hue),
    "--warning-foreground": autoForeground(darkWarningL),
    "--border": oklchAlpha(1, 0, 0, "14%"),
    // Solid input bg one step lighter than card (NOT transparent / alpha).
    // All input-shaped components — Input, Textarea, Select, Combobox — must
    // use bg-input via this token so they render identically. Previously
    // Input was transparent (showed page bg) and Select had its own
    // dark:bg-input/30 — that's the mismatch.
    "--input": oklch(0.26, Math.min(c * 0.4, 0.022), h),
    "--ring": oklch(0.552, Math.min(c * 2.7, 0.05), h),
    "--chart-1": oklch(0.488, 0.243, charts[0]),
    "--chart-2": oklch(0.696, 0.17, charts[1]),
    "--chart-3": oklch(0.769, 0.188, charts[2]),
    "--chart-4": oklch(0.627, 0.265, charts[3]),
    "--chart-5": oklch(0.645, 0.246, charts[4]),
    "--sidebar": oklch(0.21, Math.min(c * 0.5, 0.025), h),
    "--sidebar-foreground": oklch(0.985, 0, 0),
    "--sidebar-primary": oklch(0.488, 0.243, charts[0]),
    "--sidebar-primary-foreground": oklch(0.985, 0, 0),
    "--sidebar-accent": oklch(acc.lightness, acc.chroma, acc.hue),
    "--sidebar-accent-foreground": oklch(0.985, 0, 0),
    "--sidebar-border": oklchAlpha(1, 0, 0, "13%"),
    "--sidebar-ring": oklch(0.552, Math.min(c * 2.7, 0.05), h),
  };
}

/**
 * Build the @theme inline mapping block.
 * Maps --color-* to var(--*) so Tailwind utilities use CSS variable references.
 * Also maps --radius-* variants for reactive border radius.
 */
function buildThemeInlineBlock(tokens: ThemeTokens): string {
  const indent = "  ";
  const lines = [
    `${indent}--radius: var(--radius);`,
    `${indent}--radius-xs: calc(var(--radius) - 6px);`,
    `${indent}--radius-sm: calc(var(--radius) - 4px);`,
    `${indent}--radius-md: calc(var(--radius) - 2px);`,
    `${indent}--radius-lg: var(--radius);`,
    `${indent}--radius-xl: calc(var(--radius) + 4px);`,
  ];

  for (const key of Object.keys(tokens)) {
    // --background → --color-background: var(--background);
    const name = key.replace(/^--/, "");
    lines.push(`${indent}--color-${name}: var(${key});`);
  }

  return lines.join("\n");
}

export function generateThemeCSS(
  lightTokens: ThemeTokens,
  darkTokens: ThemeTokens,
  radius: number,
): string {
  const indent = "  ";

  const lightLines = Object.entries(lightTokens)
    .map(([k, v]) => `${indent}${k}: ${v};`)
    .join("\n");
  const darkLines = Object.entries(darkTokens)
    .map(([k, v]) => `${indent}${k}: ${v};`)
    .join("\n");

  const themeInline = buildThemeInlineBlock(lightTokens);

  return `:root {
  --radius: ${radius}rem;
${lightLines}
}

.dark {
${darkLines}
}

@theme inline {
${themeInline}
}`;
}
