import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import {
  UPDATE_DELIVERY_ATTEMPT_STATUS_REDACTION,
  type UpdateDeliveryAttemptStatusInput as I,
  type UpdateDeliveryAttemptStatusOutput as O,
  type IUpdateDeliveryAttemptStatusHandler,
} from "@d2/comms-app";
import { deliveryAttempt } from "../../schema/tables.js";

export class UpdateDeliveryAttemptStatus
  extends BaseHandler<I, O>
  implements IUpdateDeliveryAttemptStatusHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return UPDATE_DELIVERY_ATTEMPT_STATUS_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const updates = {
      status: input.status,
      ...(input.providerMessageId !== undefined && { providerMessageId: input.providerMessageId }),
      ...(input.error !== undefined && { error: input.error }),
      ...(input.nextRetryAt !== undefined && { nextRetryAt: input.nextRetryAt }),
    };

    const rows = await this.db
      .update(deliveryAttempt)
      .set(updates)
      .where(eq(deliveryAttempt.id, input.id))
      .returning({ id: deliveryAttempt.id });

    if (rows.length === 0) return D2Result.notFound();

    return D2Result.ok({ data: {} });
  }
}
