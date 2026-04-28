// @d2/files-domain — Pure domain types for the Files service.

// --- Constants ---
export {
  FILES_SIZE_LIMITS,
  FILES_FIELD_LIMITS,
  ALLOWED_CONTENT_TYPES,
  FILES_MESSAGING,
} from "./constants/files-constants.js";
export { FILES_ERROR_CODES } from "./constants/error-codes.js";
export type { FilesErrorCode } from "./constants/error-codes.js";

// --- Enums ---
export { FILE_STATUSES, FILE_STATUS_TRANSITIONS, isValidFileStatus } from "./enums/file-status.js";
export type { FileStatus } from "./enums/file-status.js";

export { CONTENT_CATEGORIES, isValidContentCategory } from "./enums/content-category.js";
export type { ContentCategory } from "./enums/content-category.js";

export { REJECTION_REASONS, isValidRejectionReason } from "./enums/rejection-reason.js";
export type { RejectionReason } from "./enums/rejection-reason.js";

export type { VariantSize } from "./enums/variant-size.js";

// --- Exceptions ---
export { FilesDomainError } from "./exceptions/files-domain-error.js";
export { FilesValidationError } from "./exceptions/files-validation-error.js";

// --- Entities ---
export { createFile, transitionFileStatus } from "./entities/file.js";
export type { File, CreateFileInput, TransitionFileStatusOptions } from "./entities/file.js";

// --- Value Objects ---
export { createFileVariant } from "./value-objects/file-variant.js";
export type { FileVariant, CreateFileVariantInput } from "./value-objects/file-variant.js";

export { requiresResize } from "./value-objects/variant-config.js";
export type { VariantConfig } from "./value-objects/variant-config.js";

// --- Business Rules ---
export {
  resolveContentCategory,
  isContentTypeAllowed,
  getAllowedContentTypes,
} from "./rules/content-type-rules.js";

// --- Storage Keys ---
export {
  getExtensionForContentType,
  buildRawStorageKey,
  buildVariantStorageKey,
} from "./storage-keys.js";
export type { RawStorageKeyFile, VariantStorageKeyFile } from "./storage-keys.js";
