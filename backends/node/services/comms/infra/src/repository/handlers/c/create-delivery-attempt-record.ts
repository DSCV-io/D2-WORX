import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import { isPgUniqueViolation } from "@d2/errors-pg";
import type {
  CreateDeliveryAttemptRecordInput as I,
  CreateDeliveryAttemptRecordOutput as O,
  ICreateDeliveryAttemptRecordHandler,
} from "@d2/comms-app";
import { deliveryAttempt } from "../../schema/tables.js";

export class CreateDeliveryAttemptRecord
  extends BaseHandler<I, O>
  implements ICreateDeliveryAttemptRecordHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return { suppressInput: true };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    try {
      const a = input.attempt;
      await this.db.insert(deliveryAttempt).values({
        id: a.id,
        requestId: a.requestId,
        channel: a.channel,
        recipientAddress: a.recipientAddress,
        status: a.status,
        providerMessageId: a.providerMessageId,
        error: a.error,
        attemptNumber: a.attemptNumber,
        createdAt: a.createdAt,
        nextRetryAt: a.nextRetryAt,
      });

      return D2Result.ok({ data: {} });
    } catch (err: unknown) {
      if (isPgUniqueViolation(err)) {
        return D2Result.conflict();
      }
      throw err;
    }
  }
}
