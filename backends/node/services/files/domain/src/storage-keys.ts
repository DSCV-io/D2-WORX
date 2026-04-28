/** File identity fields needed to construct raw storage keys. */
export interface RawStorageKeyFile {
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly id: string;
  readonly contentType: string;
}

/** File identity fields needed to construct variant storage keys. */
export interface VariantStorageKeyFile {
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly id: string;
}

/**
 * MIME type → file extension mapping for storage key construction.
 */
const MIME_TO_EXT: Readonly<Record<string, string>> = {
  "image/jpeg": "jpg",
  "image/png": "png",
  "image/gif": "gif",
  "image/webp": "webp",
  "image/svg+xml": "svg",
  "image/avif": "avif",
  "image/heic": "heic",
  "image/heif": "heif",
  "application/pdf": "pdf",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document": "docx",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": "xlsx",
  "text/plain": "txt",
  "text/csv": "csv",
  "video/mp4": "mp4",
  "video/webm": "webm",
  "video/quicktime": "mov",
  "video/3gpp": "3gp",
  "audio/mpeg": "mp3",
  "audio/ogg": "ogg",
  "audio/wav": "wav",
  "audio/webm": "weba",
  "audio/aac": "aac",
  "audio/mp4": "m4a",
};

/**
 * Resolves a MIME content type to its file extension.
 * Falls back to "bin" for unknown types.
 */
export function getExtensionForContentType(contentType: string): string {
  return MIME_TO_EXT[contentType] ?? "bin";
}

/**
 * Builds the MinIO storage key for a raw (unprocessed) upload.
 *
 * Format: `{contextKey}/{relatedEntityId}/{fileId}/raw.{ext}`
 */
export function buildRawStorageKey(file: RawStorageKeyFile): string {
  const ext = getExtensionForContentType(file.contentType);
  return `${file.contextKey}/${file.relatedEntityId}/${file.id}/raw.${ext}`;
}

/**
 * Builds the MinIO storage key for a processed variant.
 *
 * Format: `{contextKey}/{relatedEntityId}/{fileId}/{size}.{ext}`
 */
export function buildVariantStorageKey(
  file: VariantStorageKeyFile,
  size: string,
  contentType: string,
): string {
  const ext = getExtensionForContentType(contentType);
  return `${file.contextKey}/${file.relatedEntityId}/${file.id}/${size}.${ext}`;
}
