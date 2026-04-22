import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { createSignInEvent, type CreateSignInEventInput } from "@d2/auth-domain";
import type { ICreateSignInEventHandler } from "../../../../interfaces/repository/handlers/index.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Commands.RecordSignInEventInput;
type Output = Commands.RecordSignInEventOutput;

const schema = z.object({
  userId: zodGuid,
  successful: z.boolean(),
  ipAddress: z.string().max(45),
  userAgent: z.string().max(512),
  whoIsId: z.string().max(64).optional(),
  deviceFingerprint: z.string().max(64).optional(),
  clientFingerprint: z.string().max(64).optional(),
  serverFingerprint: z.string().max(64).optional(),
  failureReason: z.string().max(100).optional(),
});

/**
 * Records a sign-in event for audit purposes.
 * Validates input via Zod schema before persisting.
 */
export class RecordSignInEvent
  extends BaseHandler<Input, Output>
  implements Commands.IRecordSignInEventHandler
{
  private readonly createRecord: ICreateSignInEventHandler;

  override get redaction() {
    return Commands.RECORD_SIGN_IN_EVENT_REDACTION;
  }

  constructor(createRecord: ICreateSignInEventHandler, context: IHandlerContext) {
    super(context);
    this.createRecord = createRecord;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) {
      this.context.logger.warn("RecordSignInEvent validation failed", {
        errors: validation.messages,
      });
      return D2Result.bubbleFail(validation);
    }

    const createInput: CreateSignInEventInput = {
      userId: input.userId,
      successful: input.successful,
      ipAddress: input.ipAddress,
      userAgent: input.userAgent,
      whoIsId: input.whoIsId,
      deviceFingerprint: input.deviceFingerprint,
      clientFingerprint: input.clientFingerprint,
      serverFingerprint: input.serverFingerprint,
      failureReason: input.failureReason,
    };

    const event = createSignInEvent(createInput);
    const createResult = await this.createRecord.handleAsync({ event });
    if (!createResult.success) {
      this.context.logger.warn("Failed to persist sign-in event", {
        userId: input.userId,
        errors: createResult.messages,
      });
      return D2Result.bubbleFail(createResult);
    }

    return D2Result.ok({ data: { event } });
  }
}

export type {
  RecordSignInEventInput,
  RecordSignInEventOutput,
} from "../../../../interfaces/cqrs/handlers/c/record-sign-in-event.js";
