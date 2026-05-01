/**
 * Shared reactive theme state using Svelte 5 runes.
 *
 * Svelte 5 module-level rules:
 * - Cannot export `$state` that is reassigned → use object, mutate properties
 * - Cannot export `$derived` at all → export getter functions instead
 *
 * See: https://svelte.dev/e/state_invalid_export
 *      https://svelte.dev/e/derived_invalid_export
 */

import { builtInPresets, presets, loadCustomPresets, type ThemePreset } from "./theme-presets.js";
import {
  computeLightTokens,
  computeDarkTokens,
  generateThemeCSS,
  type ThemeTokens,
} from "./theme-utils.js";

// --- Editable state (single object — mutate properties, never reassign) ---

export const theme = $state({
  primaryHue: 220,
  primaryChroma: 0.12,
  primaryLightness: 0.5,
  secondaryHue: 250,
  secondaryChroma: 0.1,
  secondaryLightness: 0.5,
  accentHue: 55,
  accentChroma: 0.16,
  accentLightness: 0.65,
  destructiveHue: 25,
  destructiveChroma: 0.22,
  destructiveLightness: 0.55,
  infoHue: 220,
  infoChroma: 0.18,
  infoLightness: 0.56,
  successHue: 155,
  successChroma: 0.18,
  successLightness: 0.6,
  warningHue: 65,
  warningChroma: 0.17,
  warningLightness: 0.75,
  radius: 0.5,
});

// --- Derived (module-private, exposed via getter functions) ---

const _lightTokens = $derived.by(() =>
  computeLightTokens({
    primary: {
      hue: theme.primaryHue,
      chroma: theme.primaryChroma,
      lightness: theme.primaryLightness,
    },
    secondary: {
      hue: theme.secondaryHue,
      chroma: theme.secondaryChroma,
      lightness: theme.secondaryLightness,
    },
    accent: {
      hue: theme.accentHue,
      chroma: theme.accentChroma,
      lightness: theme.accentLightness,
    },
    destructive: {
      hue: theme.destructiveHue,
      chroma: theme.destructiveChroma,
      lightness: theme.destructiveLightness,
    },
    info: {
      hue: theme.infoHue,
      chroma: theme.infoChroma,
      lightness: theme.infoLightness,
    },
    success: {
      hue: theme.successHue,
      chroma: theme.successChroma,
      lightness: theme.successLightness,
    },
    warning: {
      hue: theme.warningHue,
      chroma: theme.warningChroma,
      lightness: theme.warningLightness,
    },
    radius: theme.radius,
  }),
);

const _darkTokens = $derived.by(() =>
  computeDarkTokens({
    primary: {
      hue: theme.primaryHue,
      chroma: theme.primaryChroma,
      lightness: theme.primaryLightness,
    },
    secondary: {
      hue: theme.secondaryHue,
      chroma: theme.secondaryChroma,
      lightness: theme.secondaryLightness,
    },
    accent: {
      hue: theme.accentHue,
      chroma: theme.accentChroma,
      lightness: theme.accentLightness,
    },
    destructive: {
      hue: theme.destructiveHue,
      chroma: theme.destructiveChroma,
      lightness: theme.destructiveLightness,
    },
    info: {
      hue: theme.infoHue,
      chroma: theme.infoChroma,
      lightness: theme.infoLightness,
    },
    success: {
      hue: theme.successHue,
      chroma: theme.successChroma,
      lightness: theme.successLightness,
    },
    warning: {
      hue: theme.warningHue,
      chroma: theme.warningChroma,
      lightness: theme.warningLightness,
    },
    radius: theme.radius,
  }),
);

const _themeCSS = $derived.by(() => generateThemeCSS(_lightTokens, _darkTokens, theme.radius));

const _activePresetName = $derived.by(() => {
  const match = presets.find(
    (p) =>
      Math.abs(p.primaryHue - theme.primaryHue) < 0.01 &&
      Math.abs(p.primaryChroma - theme.primaryChroma) < 0.001 &&
      Math.abs(p.primaryLightness - theme.primaryLightness) < 0.001 &&
      Math.abs(p.secondaryHue - theme.secondaryHue) < 0.01 &&
      Math.abs(p.secondaryChroma - theme.secondaryChroma) < 0.001 &&
      Math.abs(p.accentHue - theme.accentHue) < 0.01 &&
      Math.abs(p.accentChroma - theme.accentChroma) < 0.001 &&
      Math.abs(p.destructiveHue - theme.destructiveHue) < 0.01 &&
      Math.abs(p.radius - theme.radius) < 0.001,
  );
  return match?.name ?? null;
});

// --- Exported getters (reactive when called inside $derived / $effect / template) ---

export function getLightTokens(): ThemeTokens {
  return _lightTokens;
}

export function getDarkTokens(): ThemeTokens {
  return _darkTokens;
}

export function getThemeCSS(): string {
  return _themeCSS;
}

export function getActivePresetName(): string | null {
  return _activePresetName;
}

// --- Persistence ---

const ACTIVE_PRESET_KEY = "d2-active-theme-preset";

/** Save the active preset name to localStorage. */
function saveActivePreset(name: string | null): void {
  try {
    if (name) {
      localStorage.setItem(ACTIVE_PRESET_KEY, name);
    } else {
      localStorage.removeItem(ACTIVE_PRESET_KEY);
    }
  } catch {
    // localStorage full or unavailable
  }
}

/** Load and apply the saved preset from localStorage. Defaults to WORX. */
export function initTheme(): void {
  loadCustomPresets();
  try {
    const saved = localStorage.getItem(ACTIVE_PRESET_KEY);
    if (saved) {
      const match = presets.find((p) => p.name === saved);
      if (match) {
        applyPresetValues(match);
        return;
      }
    }
  } catch {
    // localStorage unavailable
  }
  // Default to WORX
  applyPresetValues(builtInPresets[0]);
}

// --- Actions ---

/** Internal: apply preset values without saving (used by initTheme). */
function applyPresetValues(preset: ThemePreset): void {
  theme.primaryHue = preset.primaryHue;
  theme.primaryChroma = preset.primaryChroma;
  theme.primaryLightness = preset.primaryLightness;
  theme.secondaryHue = preset.secondaryHue;
  theme.secondaryChroma = preset.secondaryChroma;
  theme.secondaryLightness = preset.secondaryLightness;
  theme.accentHue = preset.accentHue;
  theme.accentChroma = preset.accentChroma;
  theme.accentLightness = preset.accentLightness;
  theme.destructiveHue = preset.destructiveHue;
  theme.destructiveChroma = preset.destructiveChroma;
  theme.destructiveLightness = preset.destructiveLightness;
  theme.infoHue = preset.infoHue;
  theme.infoChroma = preset.infoChroma;
  theme.infoLightness = preset.infoLightness;
  theme.successHue = preset.successHue;
  theme.successChroma = preset.successChroma;
  theme.successLightness = preset.successLightness;
  theme.warningHue = preset.warningHue;
  theme.warningChroma = preset.warningChroma;
  theme.warningLightness = preset.warningLightness;
  theme.radius = preset.radius;
}

export function applyPreset(preset: ThemePreset): void {
  applyPresetValues(preset);
  saveActivePreset(preset.name);
}

export function reset(): void {
  applyPreset(builtInPresets[0]); // WORX
}
