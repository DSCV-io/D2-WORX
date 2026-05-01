export interface ThemePreset {
  name: string;
  primaryHue: number;
  primaryChroma: number;
  primaryLightness: number;
  secondaryHue: number;
  secondaryChroma: number;
  secondaryLightness: number;
  accentHue: number;
  accentChroma: number;
  accentLightness: number;
  destructiveHue: number;
  destructiveChroma: number;
  destructiveLightness: number;
  infoHue: number;
  infoChroma: number;
  infoLightness: number;
  successHue: number;
  successChroma: number;
  successLightness: number;
  warningHue: number;
  warningChroma: number;
  warningLightness: number;
  radius: number;
  builtIn?: boolean;
}

/**
 * Built-in presets — each has a complete, intentionally designed color story.
 * Brand colors (primary, secondary, accent) AND status colors are chosen
 * to harmonize, giving each theme a distinct identity.
 */
export const builtInPresets: ThemePreset[] = [
  {
    // DeCAF WORX heritage — teal-blue primary, blue-violet secondary, golden accent
    name: "WORX",
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
    builtIn: true,
  },
  {
    // Professional, understated — neutral backdrop lets standard status colors pop
    name: "Zinc",
    primaryHue: 285.885,
    primaryChroma: 0.006,
    primaryLightness: 0.21,
    secondaryHue: 260,
    secondaryChroma: 0.03,
    secondaryLightness: 0.45,
    accentHue: 310,
    accentChroma: 0.1,
    accentLightness: 0.55,
    destructiveHue: 27.325,
    destructiveChroma: 0.245,
    destructiveLightness: 0.577,
    infoHue: 262.881,
    infoChroma: 0.245,
    infoLightness: 0.546,
    successHue: 149.214,
    successChroma: 0.194,
    successLightness: 0.627,
    warningHue: 70.08,
    warningChroma: 0.189,
    warningLightness: 0.769,
    radius: 0.5,
    builtIn: true,
  },
  {
    // Corporate authority — violet-indigo primary, teal secondary, cyan accent
    name: "Ocean",
    primaryHue: 268,
    primaryChroma: 0.14,
    primaryLightness: 0.48,
    secondaryHue: 200,
    secondaryChroma: 0.1,
    secondaryLightness: 0.55,
    accentHue: 190,
    accentChroma: 0.12,
    accentLightness: 0.58,
    destructiveHue: 20,
    destructiveChroma: 0.2,
    destructiveLightness: 0.6,
    infoHue: 260,
    infoChroma: 0.2,
    infoLightness: 0.58,
    successHue: 165,
    successChroma: 0.17,
    successLightness: 0.62,
    warningHue: 75,
    warningChroma: 0.17,
    warningLightness: 0.78,
    radius: 0.5,
    builtIn: true,
  },
];

/** All presets (built-in + custom from localStorage). */
export let presets: ThemePreset[] = [...builtInPresets];

const STORAGE_KEY = "d2-design-custom-presets";

/** Fallback values for old custom presets missing secondary/accent fields. */
function migratePreset(p: Partial<ThemePreset> & { name: string }): ThemePreset {
  return {
    ...p,
    // Derive secondary/accent from primary if missing (matches old behavior)
    secondaryHue: p.secondaryHue ?? p.primaryHue ?? 0,
    secondaryChroma: p.secondaryChroma ?? (p.primaryChroma ?? 0) * 0.3,
    secondaryLightness: p.secondaryLightness ?? 0.45,
    accentHue: p.accentHue ?? p.primaryHue ?? 0,
    accentChroma: p.accentChroma ?? (p.primaryChroma ?? 0) * 0.5,
    accentLightness: p.accentLightness ?? 0.55,
    primaryHue: p.primaryHue ?? 0,
    primaryChroma: p.primaryChroma ?? 0,
    primaryLightness: p.primaryLightness ?? 0.5,
    destructiveHue: p.destructiveHue ?? 27.325,
    destructiveChroma: p.destructiveChroma ?? 0.245,
    destructiveLightness: p.destructiveLightness ?? 0.577,
    infoHue: p.infoHue ?? 262.881,
    infoChroma: p.infoChroma ?? 0.245,
    infoLightness: p.infoLightness ?? 0.546,
    successHue: p.successHue ?? 149.214,
    successChroma: p.successChroma ?? 0.194,
    successLightness: p.successLightness ?? 0.627,
    warningHue: p.warningHue ?? 70.08,
    warningChroma: p.warningChroma ?? 0.189,
    warningLightness: p.warningLightness ?? 0.769,
    radius: p.radius ?? 0.5,
    builtIn: false,
  } as ThemePreset;
}

/** Load custom presets from localStorage. */
export function loadCustomPresets(): void {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const custom: Partial<ThemePreset>[] = JSON.parse(raw);
      presets = [
        ...builtInPresets,
        ...custom.map((p) => migratePreset(p as Partial<ThemePreset> & { name: string })),
      ];
    }
  } catch {
    // Ignore corrupted data
  }
}

/** Save a new custom preset. Returns the updated presets array. */
export function saveCustomPreset(preset: Omit<ThemePreset, "builtIn">): ThemePreset[] {
  const custom = presets.filter((p) => !p.builtIn);
  // Replace if same name exists
  const idx = custom.findIndex((p) => p.name === preset.name);
  const entry = { ...preset, builtIn: false };
  if (idx >= 0) {
    custom[idx] = entry;
  } else {
    custom.push(entry);
  }
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(custom));
  } catch {
    // localStorage full or unavailable
  }
  presets = [...builtInPresets, ...custom];
  return presets;
}

/** Delete a custom preset by name. Returns the updated presets array. */
export function deleteCustomPreset(name: string): ThemePreset[] {
  const custom = presets.filter((p) => !p.builtIn && p.name !== name);
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(custom));
  } catch {
    // localStorage full or unavailable
  }
  presets = [...builtInPresets, ...custom];
  return presets;
}

/** Preview swatch color for each preset (used in the selector). */
export function presetSwatchColor(preset: ThemePreset): string {
  return `oklch(${preset.primaryLightness} ${preset.primaryChroma} ${preset.primaryHue})`;
}
