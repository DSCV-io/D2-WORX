import type { ThreadParticipant } from "../entities/thread-participant.js";
import type { Message } from "../entities/message.js";
import { PARTICIPANT_ROLE_HIERARCHY } from "../enums/participant-role.js";

/**
 * Checks whether a participant is currently active (has not left the thread).
 */
function isActive(participant: ThreadParticipant): boolean {
  return participant.leftAt == null;
}

/**
 * Gets the numeric privilege level for a participant's role.
 */
function privilegeLevel(participant: ThreadParticipant): number {
  return PARTICIPANT_ROLE_HIERARCHY[participant.role];
}

/**
 * Checks whether a participant can post a new message in the thread.
 *
 * Requirements: must be active and have at least "participant" role.
 * Observers (role=0) cannot post.
 */
export function canPostMessage(participant: ThreadParticipant): boolean {
  return (
    isActive(participant) && privilegeLevel(participant) >= PARTICIPANT_ROLE_HIERARCHY.participant
  );
}

/**
 * Checks whether a participant can edit a specific message.
 *
 * - Own messages: requires at least "participant" role + active
 * - Others' messages: requires at least "moderator" role + active
 */
export function canEditMessage(participant: ThreadParticipant, message: Message): boolean {
  if (!isActive(participant)) return false;

  const isOwnMessage =
    (participant.userId && participant.userId === message.senderUserId) ||
    (participant.contactId && participant.contactId === message.senderContactId);

  if (isOwnMessage) {
    return privilegeLevel(participant) >= PARTICIPANT_ROLE_HIERARCHY.participant;
  }

  return privilegeLevel(participant) >= PARTICIPANT_ROLE_HIERARCHY.moderator;
}

/**
 * Checks whether a participant can delete a specific message.
 *
 * Same rules as editing: own messages require "participant", others' require "moderator".
 */
export function canDeleteMessage(participant: ThreadParticipant, message: Message): boolean {
  if (!isActive(participant)) return false;

  const isOwnMessage =
    (participant.userId && participant.userId === message.senderUserId) ||
    (participant.contactId && participant.contactId === message.senderContactId);

  if (isOwnMessage) {
    return privilegeLevel(participant) >= PARTICIPANT_ROLE_HIERARCHY.participant;
  }

  return privilegeLevel(participant) >= PARTICIPANT_ROLE_HIERARCHY.moderator;
}

/**
 * Checks whether a participant can manage other participants (add/remove/change roles).
 *
 * Requires at least "moderator" role + active.
 */
export function canManageParticipants(participant: ThreadParticipant): boolean {
  return (
    isActive(participant) && privilegeLevel(participant) >= PARTICIPANT_ROLE_HIERARCHY.moderator
  );
}

/**
 * Checks whether a participant can manage the thread itself (update, archive, close).
 *
 * Requires at least "moderator" role + active.
 */
export function canManageThread(participant: ThreadParticipant): boolean {
  return (
    isActive(participant) && privilegeLevel(participant) >= PARTICIPANT_ROLE_HIERARCHY.moderator
  );
}

/**
 * Checks whether a participant can add a reaction to a message.
 *
 * Requires at least "participant" role + active. Observers cannot react.
 */
export function canAddReaction(participant: ThreadParticipant): boolean {
  return (
    isActive(participant) && privilegeLevel(participant) >= PARTICIPANT_ROLE_HIERARCHY.participant
  );
}
