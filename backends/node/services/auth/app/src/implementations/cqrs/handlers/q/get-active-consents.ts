import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { IFindActiveConsentsByUserIdHandler } from "../../../../interfaces/repository/handlers/index.js";
import { Queries } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Queries.GetActiveConsentsInput;
type Output = Queries.GetActiveConsentsOutput;

const schema = z.object({
  userId: zodGuid,
  limit: z.number().int().min(1).max(100).optional(),
  offset: z.number().int().min(0).optional(),
});

/**
 * Retrieves active (non-revoked, non-expired) emulation consents for a user.
 */
export class GetActiveConsents
  extends BaseHandler<Input, Output>
  implements Queries.IGetActiveConsentsHandler
{
  private readonly findActiveByUserId: IFindActiveConsentsByUserIdHandler;

  constructor(findActiveByUserId: IFindActiveConsentsByUserIdHandler, context: IHandlerContext) {
    super(context);
    this.findActiveByUserId = findActiveByUserId;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const findResult = await this.findActiveByUserId.handleAsync({
      userId: input.userId,
      limit: input.limit ?? 50,
      offset: input.offset ?? 0,
    });

    if (!findResult.success) return D2Result.bubbleFail(findResult);
    const consents = findResult.data?.consents ?? [];

    return D2Result.ok({ data: { consents } });
  }
}

export type {
  GetActiveConsentsInput,
  GetActiveConsentsOutput,
} from "../../../../interfaces/cqrs/handlers/q/get-active-consents.js";
