import { generateUuidV7 } from "@d2/utilities";
import type { ParticipantRole } from "../enums/participant-role.js";
import { isValidParticipantRole } from "../enums/participant-role.js";
import { CommsValidationError } from "../exceptions/comms-validation-error.js";

/**
 * A participant in a thread — either a D2 user (userId) or an external contact (contactId).
 *
 * PK is UUIDv7 `id` for consistency. DB also enforces a composite unique constraint
 * on (threadId + userId/contactId) to prevent duplicate participation.
 */
export interface ThreadParticipant {
  readonly id: string;
  readonly threadId: string;
  readonly userId?: string;
  readonly contactId?: string;
  readonly role: ParticipantRole;
  readonly notificationsMuted: boolean;
  readonly lastReadAt?: Date;
  readonly joinedAt: Date;
  readonly leftAt?: Date;
  readonly updatedAt: Date;
}

export interface CreateThreadParticipantInput {
  readonly threadId: string;
  readonly role: ParticipantRole;
  readonly id?: string;
  readonly userId?: string;
  readonly contactId?: string;
  readonly notificationsMuted?: boolean;
}

export interface UpdateThreadParticipantInput {
  readonly role?: ParticipantRole;
  readonly notificationsMuted?: boolean;
  readonly lastReadAt?: Date;
}

/**
 * Creates a new thread participant.
 */
export function createThreadParticipant(input: CreateThreadParticipantInput): ThreadParticipant {
  if (!input.threadId) {
    throw new CommsValidationError("ThreadParticipant", "threadId", input.threadId, "is required.");
  }

  const hasIdentity = !!input.userId || !!input.contactId;
  if (!hasIdentity) {
    throw new CommsValidationError(
      "ThreadParticipant",
      "identity",
      null,
      "at least one of userId or contactId is required.",
    );
  }

  if (!isValidParticipantRole(input.role)) {
    throw new CommsValidationError(
      "ThreadParticipant",
      "role",
      input.role,
      "is not a valid participant role.",
    );
  }

  const now = new Date();

  return {
    id: input.id ?? generateUuidV7(),
    threadId: input.threadId,
    userId: input.userId,
    contactId: input.contactId,
    role: input.role,
    notificationsMuted: input.notificationsMuted ?? false,
    lastReadAt: undefined,
    joinedAt: now,
    leftAt: undefined,
    updatedAt: now,
  };
}

/**
 * Updates mutable participant fields.
 */
export function updateThreadParticipant(
  participant: ThreadParticipant,
  updates: UpdateThreadParticipantInput,
): ThreadParticipant {
  let role = participant.role;
  if (updates.role !== undefined) {
    if (!isValidParticipantRole(updates.role)) {
      throw new CommsValidationError(
        "ThreadParticipant",
        "role",
        updates.role,
        "is not a valid participant role.",
      );
    }
    role = updates.role;
  }

  return {
    ...participant,
    role,
    notificationsMuted: updates.notificationsMuted ?? participant.notificationsMuted,
    lastReadAt: updates.lastReadAt !== undefined ? updates.lastReadAt : participant.lastReadAt,
    updatedAt: new Date(),
  };
}

/**
 * Marks a participant as having left the thread.
 */
export function markParticipantLeft(participant: ThreadParticipant): ThreadParticipant {
  const now = new Date();

  return {
    ...participant,
    leftAt: now,
    updatedAt: now,
  };
}
