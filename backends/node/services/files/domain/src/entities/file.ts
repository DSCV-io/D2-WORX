import { cleanStr, generateUuidV7 } from "@d2/utilities";
import type { FileStatus } from "../enums/file-status.js";
import { FILE_STATUS_TRANSITIONS } from "../enums/file-status.js";
import type { RejectionReason } from "../enums/rejection-reason.js";
import { isValidRejectionReason } from "../enums/rejection-reason.js";
import type { FileVariant } from "../value-objects/file-variant.js";
import { FILES_SIZE_LIMITS, FILES_FIELD_LIMITS } from "../constants/files-constants.js";
import { FilesValidationError } from "../exceptions/files-validation-error.js";

/**
 * File entity — tracks an uploaded file through its lifecycle.
 *
 * Starts as "pending" on upload, transitions to "processing" when picked up
 * by a worker, then to "ready" (with variants) or "rejected" (with reason).
 */
export interface File {
  readonly id: string;
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly uploaderUserId: string;
  readonly status: FileStatus;
  readonly contentType: string;
  readonly displayName: string;
  readonly sizeBytes: number;
  readonly variants?: readonly FileVariant[];
  readonly rejectionReason?: RejectionReason;
  readonly createdAt: Date;
}

export interface CreateFileInput {
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly uploaderUserId: string;
  readonly contentType: string;
  readonly displayName: string;
  readonly sizeBytes: number;
  readonly id?: string;
  readonly maxSizeBytes?: number;
}

export interface TransitionFileStatusOptions {
  readonly rejectionReason?: RejectionReason;
  readonly variants?: readonly FileVariant[];
}

/**
 * Creates a new File entity. Validates all fields.
 * Status starts as "pending", variants and rejectionReason are undefined.
 */
export function createFile(input: CreateFileInput): File {
  const contextKey = cleanStr(input.contextKey);
  if (!contextKey) {
    throw new FilesValidationError("File", "contextKey", input.contextKey, "is required.");
  }
  if (contextKey.length > FILES_FIELD_LIMITS.MAX_CONTEXT_KEY_LENGTH) {
    throw new FilesValidationError(
      "File",
      "contextKey",
      `(${contextKey.length} chars)`,
      `must not exceed ${FILES_FIELD_LIMITS.MAX_CONTEXT_KEY_LENGTH} characters.`,
    );
  }

  const relatedEntityId = cleanStr(input.relatedEntityId);
  if (!relatedEntityId) {
    throw new FilesValidationError(
      "File",
      "relatedEntityId",
      input.relatedEntityId,
      "is required.",
    );
  }
  if (relatedEntityId.length > FILES_FIELD_LIMITS.MAX_RELATED_ENTITY_ID_LENGTH) {
    throw new FilesValidationError(
      "File",
      "relatedEntityId",
      `(${relatedEntityId.length} chars)`,
      `must not exceed ${FILES_FIELD_LIMITS.MAX_RELATED_ENTITY_ID_LENGTH} characters.`,
    );
  }

  const uploaderUserId = cleanStr(input.uploaderUserId);
  if (!uploaderUserId) {
    throw new FilesValidationError("File", "uploaderUserId", input.uploaderUserId, "is required.");
  }
  if (uploaderUserId.length > FILES_FIELD_LIMITS.MAX_UPLOADER_USER_ID_LENGTH) {
    throw new FilesValidationError(
      "File",
      "uploaderUserId",
      `(${uploaderUserId.length} chars)`,
      `must not exceed ${FILES_FIELD_LIMITS.MAX_UPLOADER_USER_ID_LENGTH} characters.`,
    );
  }

  const contentType = cleanStr(input.contentType);
  if (!contentType) {
    throw new FilesValidationError("File", "contentType", input.contentType, "is required.");
  }
  if (contentType.length > FILES_FIELD_LIMITS.MAX_CONTENT_TYPE_LENGTH) {
    throw new FilesValidationError(
      "File",
      "contentType",
      `(${contentType.length} chars)`,
      `must not exceed ${FILES_FIELD_LIMITS.MAX_CONTENT_TYPE_LENGTH} characters.`,
    );
  }

  const displayName = cleanStr(input.displayName);
  if (!displayName) {
    throw new FilesValidationError("File", "displayName", input.displayName, "is required.");
  }
  if (displayName.length > FILES_FIELD_LIMITS.MAX_DISPLAY_NAME_LENGTH) {
    throw new FilesValidationError(
      "File",
      "displayName",
      `(${displayName.length} chars)`,
      `must not exceed ${FILES_FIELD_LIMITS.MAX_DISPLAY_NAME_LENGTH} characters.`,
    );
  }

  if (!Number.isFinite(input.sizeBytes) || input.sizeBytes <= 0) {
    throw new FilesValidationError(
      "File",
      "sizeBytes",
      input.sizeBytes,
      "must be a positive finite number.",
    );
  }

  const maxSize = input.maxSizeBytes ?? FILES_SIZE_LIMITS.DEFAULT_MAX_SIZE_BYTES;
  if (input.sizeBytes > maxSize) {
    throw new FilesValidationError(
      "File",
      "sizeBytes",
      input.sizeBytes,
      `must not exceed ${maxSize} bytes.`,
    );
  }

  return {
    id: input.id ?? generateUuidV7(),
    contextKey,
    relatedEntityId,
    uploaderUserId,
    status: "pending",
    contentType,
    displayName,
    sizeBytes: input.sizeBytes,
    variants: undefined,
    rejectionReason: undefined,
    createdAt: new Date(),
  };
}

/**
 * Transitions a file to a new status, enforcing the state machine.
 *
 * - "rejected" requires a valid rejectionReason.
 * - "ready" requires a non-empty variants array.
 */
export function transitionFileStatus(
  file: File,
  newStatus: FileStatus,
  options?: TransitionFileStatusOptions,
): File {
  const allowed = FILE_STATUS_TRANSITIONS[file.status];
  if (!allowed.includes(newStatus)) {
    throw new FilesValidationError(
      "File",
      "status",
      newStatus,
      `cannot transition from '${file.status}' to '${newStatus}'.`,
    );
  }

  if (newStatus === "rejected") {
    if (!options?.rejectionReason) {
      throw new FilesValidationError(
        "File",
        "rejectionReason",
        options?.rejectionReason,
        "is required when transitioning to 'rejected'.",
      );
    }
    if (!isValidRejectionReason(options.rejectionReason)) {
      throw new FilesValidationError(
        "File",
        "rejectionReason",
        options.rejectionReason,
        "is not a valid rejection reason.",
      );
    }

    return {
      ...file,
      status: newStatus,
      rejectionReason: options.rejectionReason,
    };
  }

  if (newStatus === "ready") {
    if (!options?.variants || options.variants.length === 0) {
      throw new FilesValidationError(
        "File",
        "variants",
        options?.variants,
        "must be a non-empty array when transitioning to 'ready'.",
      );
    }

    return {
      ...file,
      status: newStatus,
      variants: options.variants,
    };
  }

  return {
    ...file,
    status: newStatus,
  };
}
