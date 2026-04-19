import { and, eq, ne } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  CheckPhoneAvailabilityInput as I,
  CheckPhoneAvailabilityOutput as O,
  ICheckPhoneAvailabilityHandler,
} from "@d2/auth-app";
import { user } from "../../schema/better-auth-tables.js";

/**
 * Checks whether a phone number is available (not in use by another user).
 * `excludeUserId` optionally skips a specific user — useful when checking
 * before re-assigning a phone (the current owner doesn't conflict with itself).
 */
export class CheckPhoneAvailability
  extends BaseHandler<I, O>
  implements ICheckPhoneAvailabilityHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  get redaction(): RedactionSpec {
    return { inputFields: ["phone"] };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const where = input.excludeUserId
      ? and(eq(user.phone, input.phone), ne(user.id, input.excludeUserId))
      : eq(user.phone, input.phone);

    const rows = await this.db.select({ id: user.id }).from(user).where(where).limit(1);

    return D2Result.ok({ data: { available: rows.length === 0 } });
  }
}
