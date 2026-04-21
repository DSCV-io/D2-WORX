import type { IHandler, RedactionSpec } from "@d2/handler";
import type { DeliveryStatus } from "@d2/comms-domain";

export interface UpdateDeliveryAttemptStatusInput {
  readonly id: string;
  readonly status: DeliveryStatus;
  readonly providerMessageId?: string;
  readonly error?: string;
  readonly nextRetryAt?: Date;
}

export interface UpdateDeliveryAttemptStatusOutput {}

/**
 * `error` is a provider-supplied error string (Resend / Twilio) that may embed
 * fragments of the original recipient address (email / phone) — treat as PII.
 */
export const UPDATE_DELIVERY_ATTEMPT_STATUS_REDACTION: RedactionSpec = {
  inputFields: ["error"],
};

export interface IUpdateDeliveryAttemptStatusHandler
  extends IHandler<UpdateDeliveryAttemptStatusInput, UpdateDeliveryAttemptStatusOutput> {
  readonly redaction: RedactionSpec;
}
