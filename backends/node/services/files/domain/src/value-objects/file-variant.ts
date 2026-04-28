import { cleanStr } from "@d2/utilities";
import type { VariantSize } from "../enums/variant-size.js";
import { FILES_FIELD_LIMITS } from "../constants/files-constants.js";
import { FilesValidationError } from "../exceptions/files-validation-error.js";

/**
 * Processed variant of a file (stored as JSONB array in the File entity).
 *
 * Each variant represents a specific size rendition of the original file,
 * stored in MinIO with a unique key.
 */
export interface FileVariant {
  readonly size: VariantSize;
  readonly key: string;
  readonly width: number;
  readonly height: number;
  readonly sizeBytes: number;
  readonly contentType: string;
}

export interface CreateFileVariantInput {
  readonly size: string;
  readonly key: string;
  readonly width: number;
  readonly height: number;
  readonly sizeBytes: number;
  readonly contentType: string;
}

/**
 * Creates a validated FileVariant value object.
 */
export function createFileVariant(input: CreateFileVariantInput): FileVariant {
  const size = cleanStr(input.size);
  if (!size) {
    throw new FilesValidationError("FileVariant", "size", input.size, "is required.");
  }

  const key = cleanStr(input.key);
  if (!key) {
    throw new FilesValidationError("FileVariant", "key", input.key, "is required.");
  }
  if (key.length > FILES_FIELD_LIMITS.MAX_VARIANT_KEY_LENGTH) {
    throw new FilesValidationError(
      "FileVariant",
      "key",
      `(${key.length} chars)`,
      `must not exceed ${FILES_FIELD_LIMITS.MAX_VARIANT_KEY_LENGTH} characters.`,
    );
  }

  if (input.width < 0 || !Number.isFinite(input.width)) {
    throw new FilesValidationError(
      "FileVariant",
      "width",
      input.width,
      "must be a non-negative finite number.",
    );
  }

  if (input.height < 0 || !Number.isFinite(input.height)) {
    throw new FilesValidationError(
      "FileVariant",
      "height",
      input.height,
      "must be a non-negative finite number.",
    );
  }

  if (input.sizeBytes <= 0 || !Number.isFinite(input.sizeBytes)) {
    throw new FilesValidationError(
      "FileVariant",
      "sizeBytes",
      input.sizeBytes,
      "must be a positive finite number.",
    );
  }

  const contentType = cleanStr(input.contentType);
  if (!contentType) {
    throw new FilesValidationError("FileVariant", "contentType", input.contentType, "is required.");
  }

  return {
    size,
    key,
    width: input.width,
    height: input.height,
    sizeBytes: input.sizeBytes,
    contentType,
  };
}
