import { generateUuidV7 } from "@d2/utilities";
import { CommsValidationError } from "../exceptions/comms-validation-error.js";

/**
 * Represents the intent to deliver a message to a recipient.
 *
 * References a Message by ID — no content duplication. Recipients are normally
 * identified by contactId; Comms resolves actual email/phone via geo-client
 * at processing time. For one-shot transient sends (e.g. OTP to an unverified
 * email/phone), recipientContactId is undefined and the address came from
 * `alternativeContactInfo` on the inbound payload.
 */
export interface DeliveryRequest {
  readonly id: string;
  readonly messageId: string;
  readonly correlationId: string;
  /** Geo contact ID — undefined for transient sends to unverified addresses. */
  readonly recipientContactId?: string;
  readonly callbackTopic?: string;
  readonly createdAt: Date;
  readonly processedAt?: Date;
}

export interface CreateDeliveryRequestInput {
  readonly messageId: string;
  readonly correlationId: string;
  /** Optional — undefined for one-shot transient sends. */
  readonly recipientContactId?: string;
  readonly id?: string;
  readonly callbackTopic?: string;
}

/**
 * Creates a new delivery request. Validates messageId and correlationId.
 * recipientContactId is optional — undefined indicates a transient send
 * to an address provided via alternativeContactInfo at the API boundary.
 */
export function createDeliveryRequest(input: CreateDeliveryRequestInput): DeliveryRequest {
  if (!input.messageId) {
    throw new CommsValidationError("DeliveryRequest", "messageId", input.messageId, "is required.");
  }

  if (!input.correlationId) {
    throw new CommsValidationError(
      "DeliveryRequest",
      "correlationId",
      input.correlationId,
      "is required.",
    );
  }

  return {
    id: input.id ?? generateUuidV7(),
    messageId: input.messageId,
    correlationId: input.correlationId,
    recipientContactId: input.recipientContactId,
    callbackTopic: input.callbackTopic,
    createdAt: new Date(),
    processedAt: undefined,
  };
}

/**
 * Marks a delivery request as fully processed (all attempts terminal).
 */
export function markDeliveryRequestProcessed(request: DeliveryRequest): DeliveryRequest {
  return {
    ...request,
    processedAt: new Date(),
  };
}
