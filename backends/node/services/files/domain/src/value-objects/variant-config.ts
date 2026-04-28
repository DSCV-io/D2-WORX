/**
 * Per-variant configuration within a context key.
 *
 * Each context key defines its own set of variants with independent
 * dimension constraints. Variants without `maxDimension` are treated
 * as originals (no resize).
 */
export interface VariantConfig {
  /** Variant identifier: "thumb", "preview", "original", etc. */
  readonly name: string;
  /** Longest side pixel cap. undefined = no resize (store as-is). */
  readonly maxDimension?: number;
}

/**
 * Returns true if the variant requires image resizing.
 * Variants without `maxDimension` (or with undefined) are originals.
 */
export function requiresResize(config: VariantConfig): boolean {
  return config.maxDimension !== undefined && config.maxDimension > 0;
}
