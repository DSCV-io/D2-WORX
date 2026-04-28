import twilio from "twilio";
import type { Twilio } from "twilio";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { TK } from "@d2/i18n";
import { D2Result } from "@d2/result";
import type { ISmsProvider, SendSmsInput, SendSmsOutput } from "@d2/comms-app";

/**
 * Sends SMS via the Twilio API.
 * Implements the ISmsProvider interface from the app layer.
 */
export class TwilioSmsProvider
  extends BaseHandler<SendSmsInput, SendSmsOutput>
  implements ISmsProvider
{
  private readonly client: Twilio;
  private readonly from: string;

  constructor(accountSid: string, authToken: string, from: string, context: IHandlerContext) {
    super(context);
    this.client = twilio(accountSid, authToken);
    this.from = from;
  }

  get redaction() {
    return { inputFields: ["body", "to"] as const };
  }

  protected async executeAsync(input: SendSmsInput): Promise<D2Result<SendSmsOutput | undefined>> {
    try {
      const message = await this.client.messages.create({
        from: this.from,
        to: input.to,
        body: input.body,
      });

      return D2Result.ok({
        data: { providerMessageId: message.sid },
      });
    } catch (error: unknown) {
      // Log raw provider error for ops visibility, but never propagate it
      // to the caller — return ONLY a translated TK key so the FE can
      // resolve it without leaking provider/English copy to end users.
      this.context.logger.warn("Twilio SMS send failed", {
        error: error instanceof Error ? error.message : String(error),
      });
      return D2Result.serviceUnavailable({
        messages: [TK.comms.errors.PROVIDER_UNKNOWN],
      });
    }
  }
}
