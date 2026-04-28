import { cleanStr, generateUuidV7 } from "@d2/utilities";
import type { ThreadType } from "../enums/thread-type.js";
import { isValidThreadType } from "../enums/thread-type.js";
import type { ThreadState } from "../enums/thread-state.js";
import { THREAD_STATE_TRANSITIONS } from "../enums/thread-state.js";
import type { NotificationPolicy } from "../enums/notification-policy.js";
import { isValidNotificationPolicy } from "../enums/notification-policy.js";
import { CommsDomainError } from "../exceptions/comms-domain-error.js";
import { CommsValidationError } from "../exceptions/comms-validation-error.js";
import { THREAD_CONSTRAINTS } from "../constants/comms-constants.js";

/**
 * Regex for URL-safe thread slugs: lowercase alphanumeric + hyphens.
 */
const SLUG_REGEX = /^[a-z0-9-]+$/;

/**
 * Conversation thread — groups messages by context.
 *
 * Types: chat (DM), support (tickets), forum (topic-based), system (per-user notifications).
 * Slugs are for forum threads only — client generates slug from title and sends both.
 */
export interface Thread {
  readonly id: string;
  readonly type: ThreadType;
  readonly state: ThreadState;
  readonly title?: string;
  readonly slug?: string;
  readonly notificationPolicy: NotificationPolicy;
  readonly orgId?: string;
  readonly createdByUserId?: string;
  readonly createdAt: Date;
  readonly updatedAt: Date;
}

export interface CreateThreadInput {
  readonly type: ThreadType;
  readonly id?: string;
  readonly title?: string;
  readonly slug?: string;
  readonly notificationPolicy?: NotificationPolicy;
  readonly orgId?: string;
  readonly createdByUserId?: string;
}

export interface UpdateThreadInput {
  readonly title?: string;
  readonly slug?: string;
  readonly notificationPolicy?: NotificationPolicy;
}

function validateSlug(slug: string | undefined): string | undefined {
  if (slug == null) return undefined;

  const cleaned = cleanStr(slug);
  if (!cleaned) return undefined;

  if (!SLUG_REGEX.test(cleaned)) {
    throw new CommsValidationError(
      "Thread",
      "slug",
      cleaned,
      "must contain only lowercase letters, numbers, and hyphens ([a-z0-9-]).",
    );
  }

  if (cleaned.length > THREAD_CONSTRAINTS.MAX_TITLE_LENGTH) {
    throw new CommsValidationError(
      "Thread",
      "slug",
      `(${cleaned.length} chars)`,
      `must not exceed ${THREAD_CONSTRAINTS.MAX_TITLE_LENGTH} characters.`,
    );
  }

  return cleaned;
}

/**
 * Creates a new thread in "active" state.
 */
export function createThread(input: CreateThreadInput): Thread {
  if (!isValidThreadType(input.type)) {
    throw new CommsValidationError("Thread", "type", input.type, "is not a valid thread type.");
  }

  const title = input.title != null ? (cleanStr(input.title) ?? undefined) : undefined;
  if (title && title.length > THREAD_CONSTRAINTS.MAX_TITLE_LENGTH) {
    throw new CommsValidationError(
      "Thread",
      "title",
      `(${title.length} chars)`,
      `must not exceed ${THREAD_CONSTRAINTS.MAX_TITLE_LENGTH} characters.`,
    );
  }

  const slug = validateSlug(input.slug);

  const notificationPolicy = input.notificationPolicy ?? "all_messages";
  if (!isValidNotificationPolicy(notificationPolicy)) {
    throw new CommsValidationError(
      "Thread",
      "notificationPolicy",
      notificationPolicy,
      "is not a valid notification policy.",
    );
  }

  const now = new Date();

  return {
    id: input.id ?? generateUuidV7(),
    type: input.type,
    state: "active",
    title,
    slug,
    notificationPolicy,
    orgId: input.orgId,
    createdByUserId: input.createdByUserId,
    createdAt: now,
    updatedAt: now,
  };
}

/**
 * Updates mutable thread fields. Type and state are not updated here
 * (use transitionThreadState for state changes).
 */
export function updateThread(thread: Thread, updates: UpdateThreadInput): Thread {
  const title =
    updates.title !== undefined
      ? updates.title != null
        ? (cleanStr(updates.title) ?? undefined)
        : undefined
      : thread.title;

  if (title && title.length > THREAD_CONSTRAINTS.MAX_TITLE_LENGTH) {
    throw new CommsValidationError(
      "Thread",
      "title",
      `(${title.length} chars)`,
      `must not exceed ${THREAD_CONSTRAINTS.MAX_TITLE_LENGTH} characters.`,
    );
  }

  const slug = updates.slug !== undefined ? validateSlug(updates.slug) : thread.slug;

  let notificationPolicy = thread.notificationPolicy;
  if (updates.notificationPolicy !== undefined) {
    if (!isValidNotificationPolicy(updates.notificationPolicy)) {
      throw new CommsValidationError(
        "Thread",
        "notificationPolicy",
        updates.notificationPolicy,
        "is not a valid notification policy.",
      );
    }
    notificationPolicy = updates.notificationPolicy;
  }

  return {
    ...thread,
    title,
    slug,
    notificationPolicy,
    updatedAt: new Date(),
  };
}

/**
 * Transitions a thread to a new state according to the state machine.
 * Throws CommsDomainError if the transition is invalid.
 */
export function transitionThreadState(thread: Thread, newState: ThreadState): Thread {
  const validNextStates = THREAD_STATE_TRANSITIONS[thread.state];

  if (!validNextStates.includes(newState)) {
    throw new CommsDomainError(
      `Invalid thread state transition from '${thread.state}' to '${newState}'.`,
    );
  }

  return {
    ...thread,
    state: newState,
    updatedAt: new Date(),
  };
}
