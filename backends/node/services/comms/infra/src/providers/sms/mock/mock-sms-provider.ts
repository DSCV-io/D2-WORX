import { appendFile, mkdir } from "node:fs/promises";
import { dirname } from "node:path";
import { randomUUID } from "node:crypto";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { ISmsProvider, SendSmsInput, SendSmsOutput } from "@d2/comms-app";

/**
 * Dev/test SMS provider that appends each outgoing message to a JSONL log
 * file instead of contacting a carrier. Bypasses Twilio A2P 10DLC / Toll-Free
 * verification gates entirely.
 *
 * Each line is a self-contained JSON record: `{ ts, to, body, sid }`.
 * Follow with `tail -f <path>` from the host to watch deliveries in real time.
 *
 * Returns a synthetic provider message id so downstream tracking still works.
 */
export class MockSmsProvider
  extends BaseHandler<SendSmsInput, SendSmsOutput>
  implements ISmsProvider
{
  private readonly logPath: string;
  private dirEnsured = false;

  constructor(logPath: string, context: IHandlerContext) {
    super(context);
    this.logPath = logPath;
  }

  get redaction() {
    return { inputFields: ["body", "to"] as const };
  }

  protected async executeAsync(input: SendSmsInput): Promise<D2Result<SendSmsOutput | undefined>> {
    const sid = `MOCK-${randomUUID()}`;
    const record = {
      ts: new Date().toISOString(),
      sid,
      to: input.to,
      body: input.body,
    };

    if (!this.dirEnsured) {
      await mkdir(dirname(this.logPath), { recursive: true });
      this.dirEnsured = true;
    }
    await appendFile(this.logPath, JSON.stringify(record) + "\n", "utf8");

    this.context.logger.info("MockSmsProvider: SMS appended to log", {
      logPath: this.logPath,
      sid,
    });

    return D2Result.ok({ data: { providerMessageId: sid } });
  }
}
