import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import { z } from "zod";
import type { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { ICallOnFileProcessed } from "../../../../interfaces/outbound/handlers/call-on-file-processed.js";

type Input = Commands.NotifyFileProcessedInput;
type Output = Commands.NotifyFileProcessedOutput;

const schema = z.object({
  address: z.string().min(1).max(255),
  fileId: z.string().min(1).max(255),
  contextKey: z.string().min(1).max(255),
  relatedEntityId: z.string().min(1).max(255),
  status: z.enum(["ready", "rejected"]),
  variants: z.array(z.string()).optional(),
});

/**
 * Notifies the owning service that file processing completed.
 * Delegates to ICallOnFileProcessed for gRPC transport.
 */
export class NotifyFileProcessed
  extends BaseHandler<Input, Output>
  implements Commands.INotifyFileProcessedHandler
{
  private readonly callOnFileProcessed: ICallOnFileProcessed;

  constructor(callOnFileProcessed: ICallOnFileProcessed, context: IHandlerContext) {
    super(context);
    this.callOnFileProcessed = callOnFileProcessed;
  }

  override get redaction(): RedactionSpec {
    return { inputFields: ["address"] };
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const result = await this.callOnFileProcessed.handleAsync({
      address: input.address,
      fileId: input.fileId,
      contextKey: input.contextKey,
      relatedEntityId: input.relatedEntityId,
      status: input.status,
      variants: input.variants,
    });
    if (!result.success) return D2Result.bubbleFail(result);

    return D2Result.ok({ data: { success: true } });
  }
}
