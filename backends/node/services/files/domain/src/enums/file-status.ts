/**
 * Lifecycle status of a file.
 *
 * State machine:
 * - pending → processing | rejected
 * - processing → ready | rejected
 * - ready (terminal)
 * - rejected (terminal)
 */

export const FILE_STATUSES = ["pending", "processing", "ready", "rejected"] as const;

export type FileStatus = (typeof FILE_STATUSES)[number];

export function isValidFileStatus(value: unknown): value is FileStatus {
  return typeof value === "string" && FILE_STATUSES.includes(value as FileStatus);
}

/**
 * Valid status transitions for files.
 * Terminal states (ready, rejected) have no valid transitions.
 */
export const FILE_STATUS_TRANSITIONS: Readonly<Record<FileStatus, readonly FileStatus[]>> = {
  pending: ["processing", "rejected"],
  processing: ["ready", "rejected"],
  ready: [],
  rejected: [],
};
