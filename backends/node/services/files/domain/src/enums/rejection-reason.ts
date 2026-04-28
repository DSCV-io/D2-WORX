/**
 * Reason a file was rejected during processing.
 *
 * Set when file status transitions to "rejected".
 */

export const REJECTION_REASONS = [
  "size_exceeded",
  "size_mismatch",
  "invalid_content_type",
  "magic_bytes_mismatch",
  "content_moderation_failed",
  "processing_timeout",
  "corrupt_file",
  "virus_detected",
] as const;

export type RejectionReason = (typeof REJECTION_REASONS)[number];

export function isValidRejectionReason(value: unknown): value is RejectionReason {
  return typeof value === "string" && REJECTION_REASONS.includes(value as RejectionReason);
}
