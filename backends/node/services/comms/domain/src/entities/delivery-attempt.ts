import { generateUuidV7 } from "@d2/utilities";
import type { Channel } from "../enums/channel.js";
import { isValidChannel } from "../enums/channel.js";
import type { DeliveryStatus } from "../enums/delivery-status.js";
import { DELIVERY_STATUS_TRANSITIONS } from "../enums/delivery-status.js";
import { CommsDomainError } from "../exceptions/comms-domain-error.js";
import { CommsValidationError } from "../exceptions/comms-validation-error.js";

/**
 * Represents a single physical attempt to deliver via an external channel.
 *
 * One DeliveryRequest can spawn multiple DeliveryAttempts — both across channels
 * (one attempt per channel) and across retries (failed → new attempt row).
 */
export interface DeliveryAttempt {
  readonly id: string;
  readonly requestId: string;
  readonly channel: Channel;
  readonly recipientAddress: string;
  readonly status: DeliveryStatus;
  readonly providerMessageId?: string;
  readonly error?: string;
  readonly attemptNumber: number;
  readonly createdAt: Date;
  readonly nextRetryAt?: Date;
}

export interface CreateDeliveryAttemptInput {
  readonly requestId: string;
  readonly channel: Channel;
  readonly recipientAddress: string;
  readonly attemptNumber: number;
  readonly id?: string;
}

export interface TransitionDeliveryAttemptOptions {
  readonly providerMessageId?: string;
  readonly error?: string;
  readonly nextRetryAt?: Date;
}

/**
 * Creates a new delivery attempt in "pending" status.
 */
export function createDeliveryAttempt(input: CreateDeliveryAttemptInput): DeliveryAttempt {
  if (!input.requestId) {
    throw new CommsValidationError("DeliveryAttempt", "requestId", input.requestId, "is required.");
  }

  if (!isValidChannel(input.channel)) {
    throw new CommsValidationError(
      "DeliveryAttempt",
      "channel",
      input.channel,
      "is not a valid channel.",
    );
  }

  if (!input.recipientAddress) {
    throw new CommsValidationError(
      "DeliveryAttempt",
      "recipientAddress",
      input.recipientAddress,
      "is required.",
    );
  }

  if (!Number.isInteger(input.attemptNumber) || input.attemptNumber < 1) {
    throw new CommsValidationError(
      "DeliveryAttempt",
      "attemptNumber",
      input.attemptNumber,
      "must be a positive integer.",
    );
  }

  return {
    id: input.id ?? generateUuidV7(),
    requestId: input.requestId,
    channel: input.channel,
    recipientAddress: input.recipientAddress,
    status: "pending",
    providerMessageId: undefined,
    error: undefined,
    attemptNumber: input.attemptNumber,
    createdAt: new Date(),
    nextRetryAt: undefined,
  };
}

/**
 * Transitions a delivery attempt to a new status according to the state machine.
 * Throws CommsDomainError if the transition is invalid.
 */
export function transitionDeliveryAttemptStatus(
  attempt: DeliveryAttempt,
  newStatus: DeliveryStatus,
  options?: TransitionDeliveryAttemptOptions,
): DeliveryAttempt {
  const validNextStatuses = DELIVERY_STATUS_TRANSITIONS[attempt.status];

  if (!validNextStatuses.includes(newStatus)) {
    throw new CommsDomainError(
      `Invalid delivery status transition from '${attempt.status}' to '${newStatus}'.`,
    );
  }

  return {
    ...attempt,
    status: newStatus,
    providerMessageId: options?.providerMessageId ?? attempt.providerMessageId,
    error: options?.error ?? attempt.error,
    nextRetryAt: options?.nextRetryAt !== undefined ? options.nextRetryAt : attempt.nextRetryAt,
  };
}
