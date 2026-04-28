import { z } from "zod";
import { BaseHandler, type IHandlerContext, type RedactionSpec, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { handlePublish, type IMessagePublisher } from "@d2/messaging";
import type { INotifyHandler } from "../../interfaces/pub/notify.js";
import { COMMS_EVENTS } from "../../comms-client-constants.js";

export interface AlternativeContactInfo {
  /** Email to deliver to directly (bypasses Geo lookup). */
  readonly email?: string;
  /** Phone to deliver to directly (bypasses Geo lookup). */
  readonly phone?: string;
}

export interface NotifyInput {
  /** Geo contact ID — preferred recipient identifier (mutually exclusive with alternativeContactInfo). */
  readonly recipientContactId?: string;
  /**
   * One-shot transient recipient — bypasses Geo contact lookup. Use when
   * sending to addresses that aren't yet contacts (e.g., OTP for unverified
   * new email/phone). Either recipientContactId OR alternativeContactInfo
   * MUST be provided (not both).
   */
  readonly alternativeContactInfo?: AlternativeContactInfo;
  /** Email subject, SMS prefix, push title. */
  readonly title: string;
  /** Markdown content — rendered to HTML for email. */
  readonly content: string;
  /** Plain text — SMS body, email fallback. */
  readonly plaintext: string;
  /** Explicit channel override. Empty/undefined = resolve from preferences (only valid with recipientContactId). */
  readonly channels?: ("email" | "sms")[];
  /** Default "normal". "urgent" = bypass prefs, force all channels. */
  readonly urgency?: "normal" | "urgent";
  /** Idempotency key for deduplication. */
  readonly correlationId: string;
  /** Source service identifier (e.g. "auth", "billing"). */
  readonly senderService: string;
  /** Arbitrary key-value metadata for future use. */
  readonly metadata?: Record<string, unknown>;
}

export interface NotifyOutput {}

const notifySchema = z
  .object({
    recipientContactId: zodGuid.optional(),
    alternativeContactInfo: z
      .object({
        email: z.string().email().max(254).optional(),
        phone: z
          .string()
          .regex(/^\d{7,15}$/)
          .optional(),
      })
      .refine((v) => !!(v.email || v.phone), {
        message: "alternativeContactInfo must include at least one of email or phone",
      })
      .optional(),
    title: z.string().min(1).max(255),
    content: z.string().min(1).max(50_000),
    plaintext: z.string().min(1).max(50_000),
    channels: z
      .array(z.enum(["email", "sms"]))
      .optional()
      .default([]),
    urgency: z.enum(["normal", "urgent"]).optional().default("normal"),
    correlationId: z.string().min(1).max(36),
    senderService: z.string().min(1).max(50),
    metadata: z.record(z.string(), z.unknown()).optional(),
  })
  .refine((v) => !!v.recipientContactId !== !!v.alternativeContactInfo, {
    message: "Exactly one of recipientContactId or alternativeContactInfo must be provided",
  });

/**
 * Validates and publishes a notification request to the Comms service
 * via RabbitMQ. The Comms service receives a universal message shape,
 * resolves the contact's address, picks channels, renders markdown to
 * HTML, and delivers.
 *
 * When no publisher is provided (local dev, tests without RabbitMQ),
 * the handler logs the notification and returns success.
 */
export class Notify extends BaseHandler<NotifyInput, NotifyOutput> implements INotifyHandler {
  private readonly publisher: IMessagePublisher | undefined;

  override get redaction(): RedactionSpec {
    return { inputFields: ["content", "plaintext", "alternativeContactInfo"] };
  }

  constructor(context: IHandlerContext, publisher?: IMessagePublisher) {
    super(context);
    this.publisher = publisher;
  }

  protected async executeAsync(input: NotifyInput): Promise<D2Result<NotifyOutput | undefined>> {
    const validation = this.validateInput(notifySchema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    if (!this.publisher) {
      this.context.logger.info("No publisher configured — notification logged but not sent", {
        recipientContactId: input.recipientContactId,
        usingAlternative: !!input.alternativeContactInfo,
        title: input.title,
        senderService: input.senderService,
        correlationId: input.correlationId,
      });
      return D2Result.ok({ data: {} });
    }

    const publishResult = await handlePublish(
      this.publisher,
      {
        exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE,
        routingKey: "",
      },
      {
        recipientContactId: input.recipientContactId,
        alternativeContactInfo: input.alternativeContactInfo,
        title: input.title,
        content: input.content,
        plaintext: input.plaintext,
        channels: input.channels ?? [],
        urgency: input.urgency ?? "normal",
        correlationId: input.correlationId,
        senderService: input.senderService,
        metadata: input.metadata,
      },
    );

    if (!publishResult.success) return D2Result.bubbleFail(publishResult);

    return D2Result.ok({ data: {} });
  }
}
