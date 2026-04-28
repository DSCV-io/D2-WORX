import { readFileSync } from "node:fs";
import { marked } from "marked";
import type { Channel } from "@d2/comms-domain";
import type { IEmailProvider, SendEmailInput } from "../../../../interfaces/providers/index.js";
import type { ISmsProvider } from "../../../../interfaces/providers/index.js";

/** Input for channel dispatch — content fields needed by all channels. */
export interface DispatchInput {
  readonly address: string;
  readonly title: string;
  readonly content: string;
  readonly plainTextContent: string;
}

/** Result of a channel dispatch — success/failure + provider metadata. */
export interface DispatchResult {
  readonly success: boolean;
  readonly providerMessageId?: string;
  readonly error?: string;
}

/**
 * Strategy interface for channel-specific delivery.
 * Each implementation handles content transformation and provider invocation.
 * The Deliver handler owns orchestration (attempt creation, persistence, retry).
 */
export interface IChannelDispatcher {
  readonly channel: Channel;
  dispatch(input: DispatchInput): Promise<DispatchResult>;
}

const DEFAULT_FOOTER_TEXT = "D2-WORX";

/** Creates an email HTML wrapper with the given footer brand text. */
export function createEmailWrapper(footerText: string): string {
  const year = new Date().getFullYear();
  return `<!DOCTYPE html>
<html><body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 600px; margin: 0 auto; padding: 0; background-color: #f4f5f7;">
  <table width="100%" cellpadding="0" cellspacing="0" style="max-width: 600px; margin: 0 auto; background-color: #ffffff;">
    <tr>
      <td style="padding: 24px 24px 16px; border-bottom: 3px solid #3a5a82;">
        <table cellpadding="0" cellspacing="0">
          <tr>
            <td style="background-color: #3a5a82; color: #fafafa; font-size: 13px; font-weight: bold; width: 30px; height: 30px; text-align: center; vertical-align: middle; border-radius: 6px; line-height: 30px;">DW</td>
            <td style="padding-left: 10px; font-size: 17px; font-weight: 600; color: #3a5a82; letter-spacing: 0.5px;">${footerText}</td>
          </tr>
        </table>
      </td>
    </tr>
    <tr>
      <td style="padding: 32px 24px;">
        <h2 style="margin: 0 0 16px; font-size: 20px; color: #3a5a82;">{{title}}</h2>
        <div style="font-size: 15px; line-height: 1.6; color: #374151;">{{body}}</div>
      </td>
    </tr>
    <tr>
      <td style="padding: 0 24px;">
        <hr style="border: none; border-top: 1px solid #e2e5ea; margin: 0;" />
      </td>
    </tr>
    <tr>
      <td style="padding: 20px 24px 24px; text-align: center;">
        <p style="margin: 0 0 4px; color: #7b7f92; font-size: 12px;">This is an automated message from ${footerText}.</p>
        <p style="margin: 0; color: #7b7f92; font-size: 11px;">\u00A9 ${year} DCSV</p>
        {{unsubscribeUrl}}
      </td>
    </tr>
  </table>
</body></html>`;
}

/**
 * Loads an email template from a file path, or falls back to the built-in default.
 * The file must contain `{{title}}`, `{{body}}`, and `{{unsubscribeUrl}}` placeholders.
 */
export function loadEmailWrapper(footerText: string, templatePath?: string): string {
  if (templatePath) {
    try {
      return readFileSync(templatePath, "utf-8");
    } catch (err: unknown) {
      // File missing or unreadable — fall through to built-in default
      console.warn(
        `loadEmailWrapper: failed to read template at "${templatePath}", falling back to default`,
        { error: err instanceof Error ? err.message : String(err) },
      );
    }
  }
  return createEmailWrapper(footerText);
}

/** Default email HTML wrapper. */
const DEFAULT_EMAIL_WRAPPER = createEmailWrapper(DEFAULT_FOOTER_TEXT);

/**
 * Email channel dispatcher.
 * Renders markdown to sanitized HTML, wraps in email template, sends via provider.
 */
export class EmailDispatcher implements IChannelDispatcher {
  readonly channel: Channel = "email";
  private readonly provider: IEmailProvider;
  private readonly emailWrapper: string;

  constructor(provider: IEmailProvider, emailWrapper?: string) {
    this.provider = provider;
    this.emailWrapper = emailWrapper ?? DEFAULT_EMAIL_WRAPPER;
  }

  async dispatch(input: DispatchInput): Promise<DispatchResult> {
    let renderedBody: string;
    try {
      renderedBody = renderMarkdownToHtml(input.content);
    } catch (err) {
      const message = err instanceof Error ? err.message : "Unknown rendering error";
      return { success: false, error: `Markdown rendering failed: ${message}` };
    }

    const html = renderTemplate(this.emailWrapper, {
      title: input.title,
      body: renderedBody,
      unsubscribeUrl: "", // TODO: wire unsubscribe URL (PLANNING.md issue #81)
    });

    const sendInput: SendEmailInput = {
      to: input.address,
      subject: input.title,
      html,
      plainText: input.plainTextContent,
    };

    const sendResult = await this.provider.handleAsync(sendInput);

    if (sendResult.success && sendResult.data) {
      return { success: true, providerMessageId: sendResult.data.providerMessageId };
    }
    return { success: false, error: sendResult.messages?.join("; ") ?? "Email send failed" };
  }
}

/**
 * SMS channel dispatcher.
 * Sends plaintext content via SMS provider.
 */
export class SmsDispatcher implements IChannelDispatcher {
  readonly channel: Channel = "sms";
  private readonly provider: ISmsProvider;

  constructor(provider: ISmsProvider) {
    this.provider = provider;
  }

  async dispatch(input: DispatchInput): Promise<DispatchResult> {
    const sendResult = await this.provider.handleAsync({
      to: input.address,
      body: input.plainTextContent,
    });

    if (sendResult.success && sendResult.data) {
      return { success: true, providerMessageId: sendResult.data.providerMessageId };
    }
    return { success: false, error: sendResult.messages?.join("; ") ?? "SMS send failed" };
  }
}

/** Simple {{key}} template interpolation — no escaping (callers escape inputs as needed). */
function renderTemplate(template: string, vars: Record<string, string>): string {
  return template.replace(/\{\{(\w+)\}\}/g, (_, key: string) => vars[key] ?? "");
}

/**
 * Renders markdown to HTML using `marked`.
 * No escaping — callers are responsible for sanitizing user-provided values
 * before embedding them in the markdown content.
 */
function renderMarkdownToHtml(markdown: string): string {
  return marked.parse(markdown, { async: false }) as string;
}
