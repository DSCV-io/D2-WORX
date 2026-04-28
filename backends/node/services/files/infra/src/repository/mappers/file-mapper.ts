import { truthyOrUndefined } from "@d2/utilities";
import type { File, FileStatus, RejectionReason, FileVariant } from "@d2/files-domain";
import type { FileRow } from "../schema/types.js";

/**
 * Maps a Drizzle file row to a File domain entity.
 *
 * Uses {@link truthyOrUndefined} on optional string columns so empty strings
 * and whitespace-only DB values become `undefined` in the domain model.
 */
export function toFile(row: FileRow): File {
  const rejectionReason = truthyOrUndefined(row.rejectionReason) as RejectionReason | undefined;
  return {
    id: row.id,
    contextKey: row.contextKey,
    relatedEntityId: row.relatedEntityId,
    uploaderUserId: row.uploaderUserId,
    status: row.status as FileStatus,
    contentType: row.contentType,
    displayName: row.displayName,
    sizeBytes: row.sizeBytes,
    variants: row.variants ? (row.variants as FileVariant[]) : undefined,
    rejectionReason,
    createdAt: row.createdAt,
  };
}
