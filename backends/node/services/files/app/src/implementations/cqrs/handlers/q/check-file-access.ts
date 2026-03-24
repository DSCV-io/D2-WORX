import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import { z } from "zod";
import type { Queries } from "../../../../interfaces/cqrs/handlers/index.js";
import type { ICallCanAccess } from "../../../../interfaces/outbound/handlers/call-can-access.js";

type Input = Queries.CheckFileAccessInput;
type Output = Queries.CheckFileAccessOutput;

const schema = z.object({
  address: z.string().min(1).max(255),
  contextKey: z.string().min(1).max(255),
  relatedEntityId: z.string().min(1).max(255),
  requestingUserId: z.string().min(1).max(255),
  requestingOrgId: z.string().min(1).max(255).optional(),
  action: z.enum(["upload", "read"]),
});

/**
 * Checks file access by calling the owning service's gRPC CanAccess RPC.
 * Delegates to ICallCanAccess for gRPC transport.
 *
 * Fail-closed: missing or malformed response → allowed: false.
 */
export class CheckFileAccess
  extends BaseHandler<Input, Output>
  implements Queries.ICheckFileAccessHandler
{
  private readonly callCanAccess: ICallCanAccess;

  constructor(callCanAccess: ICallCanAccess, context: IHandlerContext) {
    super(context);
    this.callCanAccess = callCanAccess;
  }

  override get redaction(): RedactionSpec {
    return { inputFields: ["address"] };
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const result = await this.callCanAccess.handleAsync({
      address: input.address,
      contextKey: input.contextKey,
      relatedEntityId: input.relatedEntityId,
      requestingUserId: input.requestingUserId,
      requestingOrgId: input.requestingOrgId,
      action: input.action,
    });
    if (!result.success) return D2Result.bubbleFail(result);

    // Fail-closed: missing or malformed response → allowed: false
    const allowed = result.data?.allowed === true;

    return D2Result.ok({ data: { allowed } });
  }
}
