import type { IHandler } from "@d2/handler";
import type { VariantConfig } from "@d2/files-domain";

export interface ProcessedVariant {
  readonly size: string;
  readonly buffer: Buffer;
  readonly width: number;
  readonly height: number;
  readonly sizeBytes: number;
  readonly contentType: string;
}

export interface ProcessVariantsInput {
  readonly buffer: Buffer;
  readonly contentType: string;
  readonly variants: readonly VariantConfig[];
}

export interface ProcessVariantsOutput {
  readonly variants: readonly ProcessedVariant[];
}

/** Image processing provider (sharp) — only called for variants that require resizing. */
export type IProcessVariants = IHandler<ProcessVariantsInput, ProcessVariantsOutput>;
