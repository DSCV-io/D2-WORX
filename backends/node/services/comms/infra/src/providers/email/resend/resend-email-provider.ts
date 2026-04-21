import { Resend } from "resend";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { TK } from "@d2/i18n";
import { D2Result } from "@d2/result";
import type { IEmailProvider, SendEmailInput, SendEmailOutput } from "@d2/comms-app";

/**
 * Sends emails via the Resend API.
 * Implements the IEmailProvider interface from the app layer.
 */
export class ResendEmailProvider
  extends BaseHandler<SendEmailInput, SendEmailOutput>
  implements IEmailProvider
{
  private readonly client: Resend;
  private readonly from: string;

  constructor(apiKey: string, from: string, context: IHandlerContext) {
    super(context);
    this.client = new Resend(apiKey);
    this.from = from;
  }

  get redaction() {
    return { inputFields: ["html", "plainText", "to", "replyTo"] as const };
  }

  protected async executeAsync(
    input: SendEmailInput,
  ): Promise<D2Result<SendEmailOutput | undefined>> {
    const { data, error } = await this.client.emails.send({
      from: this.from,
      to: input.to,
      subject: input.subject,
      html: input.html,
      text: input.plainText,
      replyTo: input.replyTo,
    });

    if (error) {
      // Log raw provider error for ops visibility, but never propagate it
      // to the caller — return ONLY a translated TK key so the FE can
      // resolve it without leaking provider/English copy to end users.
      this.context.logger.warn("Resend email send failed", { error: error.message });
      return D2Result.serviceUnavailable({
        messages: [TK.comms.errors.PROVIDER_UNKNOWN],
      });
    }

    return D2Result.ok({
      data: { providerMessageId: data!.id },
    });
  }
}
