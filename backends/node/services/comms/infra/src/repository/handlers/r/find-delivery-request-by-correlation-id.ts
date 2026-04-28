import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  FindDeliveryRequestByCorrelationIdInput as I,
  FindDeliveryRequestByCorrelationIdOutput as O,
  IFindDeliveryRequestByCorrelationIdHandler,
} from "@d2/comms-app";
import { deliveryRequest } from "../../schema/tables.js";
import { toDeliveryRequest } from "./find-delivery-request-by-id.js";

export class FindDeliveryRequestByCorrelationId
  extends BaseHandler<I, O>
  implements IFindDeliveryRequestByCorrelationIdHandler
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
      .where(eq(deliveryRequest.correlationId, input.correlationId))
      .limit(1);

    const row = rows[0];
    return D2Result.ok({
      data: { request: row ? toDeliveryRequest(row) : undefined },
    });
  }
}
