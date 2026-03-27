import type { IHandler, RedactionSpec } from "@d2/handler";
import type { DeliveryAttempt } from "@d2/comms-domain";

export interface DeliverInput {
  readonly senderService: string;
  readonly title: string;
  readonly content: string;
  readonly plainTextContent: string;
  readonly channels?: ("email" | "sms")[];
  readonly urgency?: "normal" | "urgent";
  readonly recipientContactId: string;
  readonly correlationId: string;
  readonly metadata?: Record<string, unknown>;
}

export interface DeliverOutput {
  readonly messageId: string;
  readonly requestId: string;
  readonly attempts: DeliveryAttempt[];
}

/** Recommended redaction for Deliver handlers. */
export const DELIVER_REDACTION: RedactionSpec = {
  inputFields: ["content", "plainTextContent", "title"],
  suppressOutput: true,
};

/** Handler for the core delivery orchestrator. Requires redaction (I/O contains content/PII). */
export interface IDeliverHandler extends IHandler<DeliverInput, DeliverOutput> {
  readonly redaction: RedactionSpec;
}
