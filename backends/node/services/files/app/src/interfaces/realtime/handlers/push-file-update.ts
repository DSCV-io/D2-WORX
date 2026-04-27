import type { IHandler, RedactionSpec } from "@d2/handler";

export interface PushFileUpdateInput {
  /** The uploader's user ID — push targets `user:{uploaderUserId}` channel. */
  readonly uploaderUserId: string;
  /** The file that was processed. */
  readonly fileId: string;
  /** The context key of the file (e.g., "user_avatar", "thread_attachment"). */
  readonly contextKey: string;
  /** Final status after processing. */
  readonly status: "ready" | "rejected";
  /** Rejection reason if status is "rejected". */
  readonly rejectionReason?: string;
  /** Variant names available (e.g., ["original", "thumb", "medium"]). */
  readonly variants?: readonly string[];
}

export interface PushFileUpdateOutput {
  /** Whether the push was delivered (false if user is not connected). */
  readonly delivered: boolean;
}

/**
 * Inputs are opaque identifiers (uploaderUserId, fileId, contextKey, status,
 * rejectionReason, variant names) — no PII is logged. Output is a boolean.
 * Constant exists so this handler explicitly opts-in to the project-wide
 * RedactionSpec convention, mirroring `CALL_CAN_ACCESS_REDACTION` and
 * `CALL_ON_FILE_PROCESSED_REDACTION`.
 */
export const PUSH_FILE_UPDATE_REDACTION: RedactionSpec = {};

/** Pushes a file processing update to a connected client via the SignalR gateway. */
export interface IPushFileUpdate extends IHandler<PushFileUpdateInput, PushFileUpdateOutput> {
  readonly redaction: RedactionSpec;
}
