import { cleanStr, generateUuidV7 } from "@d2/utilities";
import type { Channel } from "../enums/channel.js";
import { isValidChannel } from "../enums/channel.js";
import type { ContentFormat } from "../enums/content-format.js";
import { isValidContentFormat } from "../enums/content-format.js";
import type { Urgency } from "../enums/urgency.js";
import { isValidUrgency } from "../enums/urgency.js";
import { CommsValidationError } from "../exceptions/comms-validation-error.js";
import { THREAD_CONSTRAINTS } from "../constants/comms-constants.js";

/**
 * Universal content entity — source of truth for ALL communications.
 *
 * Standalone messages (threadId=undefined) represent transactional sends (password reset, invoices).
 * Threaded messages (threadId set) represent conversational content (chat, support, forum).
 * Notification messages are posted to a per-user system thread with relatedEntityId set.
 */
export interface Message {
  readonly id: string;
  readonly threadId?: string;
  readonly parentMessageId?: string;
  readonly senderUserId?: string;
  readonly senderContactId?: string;
  readonly senderService?: string;
  readonly title?: string;
  readonly content: string;
  readonly plainTextContent: string;
  readonly contentFormat: ContentFormat;
  readonly channels: Channel[];
  readonly urgency: Urgency;
  readonly relatedEntityId?: string;
  readonly relatedEntityType?: string;
  readonly metadata?: Record<string, unknown>;
  readonly editedAt?: Date;
  readonly deletedAt?: Date;
  readonly createdAt: Date;
  readonly updatedAt: Date;
}

export interface CreateMessageInput {
  readonly content: string;
  readonly plainTextContent: string;
  readonly id?: string;
  readonly threadId?: string;
  readonly parentMessageId?: string;
  readonly senderUserId?: string;
  readonly senderContactId?: string;
  readonly senderService?: string;
  readonly title?: string;
  readonly contentFormat?: ContentFormat;
  readonly channels?: Channel[];
  readonly urgency?: Urgency;
  readonly relatedEntityId?: string;
  readonly relatedEntityType?: string;
  readonly metadata?: Record<string, unknown>;
}

/**
 * Creates a new message. Validates content, sender, and enum fields.
 */
export function createMessage(input: CreateMessageInput): Message {
  const content = cleanStr(input.content);
  if (!content) {
    throw new CommsValidationError("Message", "content", input.content, "is required.");
  }
  if (content.length > THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH) {
    throw new CommsValidationError(
      "Message",
      "content",
      `(${content.length} chars)`,
      `must not exceed ${THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH} characters.`,
    );
  }

  const plainTextContent = cleanStr(input.plainTextContent);
  if (!plainTextContent) {
    throw new CommsValidationError(
      "Message",
      "plainTextContent",
      input.plainTextContent,
      "is required.",
    );
  }
  if (plainTextContent.length > THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH) {
    throw new CommsValidationError(
      "Message",
      "plainTextContent",
      `(${plainTextContent.length} chars)`,
      `must not exceed ${THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH} characters.`,
    );
  }

  const contentFormat = input.contentFormat ?? "markdown";
  if (!isValidContentFormat(contentFormat)) {
    throw new CommsValidationError(
      "Message",
      "contentFormat",
      contentFormat,
      "is not a valid content format.",
    );
  }

  const urgency = input.urgency ?? "normal";
  if (!isValidUrgency(urgency)) {
    throw new CommsValidationError("Message", "urgency", urgency, "is not a valid urgency level.");
  }

  const title = input.title != null ? (cleanStr(input.title) ?? undefined) : undefined;
  if (title && title.length > THREAD_CONSTRAINTS.MAX_TITLE_LENGTH) {
    throw new CommsValidationError(
      "Message",
      "title",
      `(${title.length} chars)`,
      `must not exceed ${THREAD_CONSTRAINTS.MAX_TITLE_LENGTH} characters.`,
    );
  }

  const channels = input.channels ?? [];
  for (const ch of channels) {
    if (!isValidChannel(ch)) {
      throw new CommsValidationError("Message", "channels", ch, "is not a valid channel.");
    }
  }

  // At least one sender required
  const hasSender = !!input.senderUserId || !!input.senderContactId || !!input.senderService;
  if (!hasSender) {
    throw new CommsValidationError(
      "Message",
      "sender",
      null,
      "at least one of senderUserId, senderContactId, or senderService is required.",
    );
  }

  const now = new Date();

  return {
    id: input.id ?? generateUuidV7(),
    threadId: input.threadId,
    parentMessageId: input.parentMessageId,
    senderUserId: input.senderUserId,
    senderContactId: input.senderContactId,
    senderService: input.senderService,
    title,
    content,
    plainTextContent,
    contentFormat,
    channels,
    urgency,
    relatedEntityId: input.relatedEntityId,
    relatedEntityType: input.relatedEntityType,
    metadata: input.metadata,
    editedAt: undefined,
    deletedAt: undefined,
    createdAt: now,
    updatedAt: now,
  };
}

/**
 * Edits message content. Sets editedAt to track content modifications.
 */
export function editMessage(message: Message, content: string, plainTextContent: string): Message {
  const cleaned = cleanStr(content);
  if (!cleaned) {
    throw new CommsValidationError("Message", "content", content, "is required.");
  }
  if (cleaned.length > THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH) {
    throw new CommsValidationError(
      "Message",
      "content",
      `(${cleaned.length} chars)`,
      `must not exceed ${THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH} characters.`,
    );
  }

  const cleanedPlainText = cleanStr(plainTextContent);
  if (!cleanedPlainText) {
    throw new CommsValidationError("Message", "plainTextContent", plainTextContent, "is required.");
  }
  if (cleanedPlainText.length > THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH) {
    throw new CommsValidationError(
      "Message",
      "plainTextContent",
      `(${cleanedPlainText.length} chars)`,
      `must not exceed ${THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH} characters.`,
    );
  }

  const now = new Date();

  return {
    ...message,
    content: cleaned,
    plainTextContent: cleanedPlainText,
    editedAt: now,
    updatedAt: now,
  };
}

/**
 * Soft-deletes a message by setting deletedAt.
 */
export function softDeleteMessage(message: Message): Message {
  const now = new Date();

  return {
    ...message,
    deletedAt: now,
    updatedAt: now,
  };
}
