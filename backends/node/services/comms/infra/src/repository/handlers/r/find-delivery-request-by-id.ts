import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  FindDeliveryRequestByIdInput as I,
  FindDeliveryRequestByIdOutput as O,
  IFindDeliveryRequestByIdHandler,
} from "@d2/comms-app";
import type { DeliveryRequest } from "@d2/comms-domain";
import { deliveryRequest } from "../../schema/tables.js";
import type { DeliveryRequestRow } from "../../schema/types.js";

export class FindDeliveryRequestById
  extends BaseHandler<I, O>
  implements IFindDeliveryRequestByIdHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const rows = await this.db
      .select()
      .from(deliveryRequest)
      .where(eq(deliveryRequest.id, input.id))
      .limit(1);

    const row = rows[0];
    if (!row) {
      return D2Result.notFound();
    }

    return D2Result.ok({ data: { request: toDeliveryRequest(row) } });
  }
}

export function toDeliveryRequest(row: DeliveryRequestRow): DeliveryRequest {
  return {
    id: row.id,
    messageId: row.messageId,
    correlationId: row.correlationId,
    recipientContactId: row.recipientContactId ?? undefined,
    callbackTopic: row.callbackTopic ?? undefined,
    createdAt: row.createdAt,
    processedAt: row.processedAt ?? undefined,
  };
}
