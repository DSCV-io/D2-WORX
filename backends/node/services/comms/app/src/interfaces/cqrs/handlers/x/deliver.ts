import type { IHandler, RedactionSpec } from "@d2/handler";
import type { DeliveryAttempt } from "@d2/comms-domain";

/**
 * One-shot transient recipient — bypasses Geo contact lookup.
 * Use when sending to addresses that aren't yet contacts (e.g., OTP for unverified
 * new email/phone). Either recipientContactId OR alternativeContactInfo MUST be
 * provided (not both).
 */
export interface AlternativeContactInfo {
  readonly email?: string;
  readonly phone?: string;
}

export interface DeliverInput {
  readonly senderService: string;
  readonly title: string;
  readonly content: string;
  readonly plainTextContent: string;
  readonly channels?: ("email" | "sms")[];
  readonly urgency?: "normal" | "urgent";
  /** Geo contact ID — preferred recipient (mutually exclusive with alternativeContactInfo). */
  readonly recipientContactId?: string;
  /** One-shot transient recipient — see AlternativeContactInfo doc. */
  readonly alternativeContactInfo?: AlternativeContactInfo;
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
  inputFields: ["content", "plainTextContent", "title", "alternativeContactInfo"],
  suppressOutput: true,
};

/** Handler for the core delivery orchestrator. Requires redaction (I/O contains content/PII). */
export interface IDeliverHandler extends IHandler<DeliverInput, DeliverOutput> {
  readonly redaction: RedactionSpec;
}
