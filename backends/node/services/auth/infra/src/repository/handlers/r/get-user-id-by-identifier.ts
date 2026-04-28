import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  GetUserIdByIdentifierInput as I,
  GetUserIdByIdentifierOutput as O,
  IGetUserIdByIdentifierHandler,
} from "@d2/auth-app";
import { user } from "../../schema/better-auth-tables.js";

/**
 * Resolves the userId for a sign-in identifier (email OR username).
 *
 * Used by the failed-sign-in audit path so we can record `sign_in_event` rows
 * with `successful: false` against the correct user. Returns `userId: undefined`
 * when no user matches — those failed attempts are dropped from the audit table
 * (the throttle/rate-limit layer still tracks them by hashed identifier).
 */
export class GetUserIdByIdentifier
  extends BaseHandler<I, O>
  implements IGetUserIdByIdentifierHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  get redaction(): RedactionSpec {
    return { inputFields: ["email", "username"], suppressOutput: true };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    if (input.email) {
      const rows = await this.db
        .select({ id: user.id })
        .from(user)
        .where(eq(user.email, input.email))
        .limit(1);
      return D2Result.ok({ data: { userId: rows[0]?.id } });
    }
    if (input.username) {
      const rows = await this.db
        .select({ id: user.id })
        .from(user)
        .where(eq(user.username, input.username))
        .limit(1);
      return D2Result.ok({ data: { userId: rows[0]?.id } });
    }
    return D2Result.ok({ data: { userId: undefined } });
  }
}
