import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  FindDeliveryAttemptsByRequestIdInput as I,
  FindDeliveryAttemptsByRequestIdOutput as O,
  IFindDeliveryAttemptsByRequestIdHandler,
} from "@d2/comms-app";
import type { DeliveryAttempt, Channel, DeliveryStatus } from "@d2/comms-domain";
import { deliveryAttempt } from "../../schema/tables.js";
import type { DeliveryAttemptRow } from "../../schema/types.js";

export class FindDeliveryAttemptsByRequestId
  extends BaseHandler<I, O>
  implements IFindDeliveryAttemptsByRequestIdHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return { suppressOutput: true };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const rows = await this.db
      .select()
      .from(deliveryAttempt)
      .where(eq(deliveryAttempt.requestId, input.requestId));

    return D2Result.ok({
      data: { attempts: rows.map(toDeliveryAttempt) },
    });
  }
}

export function toDeliveryAttempt(row: DeliveryAttemptRow): DeliveryAttempt {
  return {
    id: row.id,
    requestId: row.requestId,
    channel: row.channel as Channel,
    recipientAddress: row.recipientAddress,
    status: row.status as DeliveryStatus,
    providerMessageId: row.providerMessageId ?? undefined,
    error: row.error ?? undefined,
    attemptNumber: row.attemptNumber,
    createdAt: row.createdAt,
    nextRetryAt: row.nextRetryAt ?? undefined,
  };
}
