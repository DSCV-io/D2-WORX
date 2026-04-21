import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import {
  UPDATE_USER_PHONE_REDACTION,
  type UpdateUserPhoneInput as I,
  type UpdateUserPhoneOutput as O,
  type IUpdateUserPhoneHandler as IHandler,
} from "@d2/auth-app";
import { user } from "../../schema/better-auth-tables.js";

/**
 * Updates a user's phone + phoneVerified flag atomically.
 *
 * Pass `phone: null` + `phoneVerified: false` to remove a phone.
 * Callers should validate phone uniqueness BEFORE calling — this handler will
 * return a PG constraint error (409) on collision (partial unique index).
 */
export class UpdateUserPhone extends BaseHandler<I, O> implements IHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return UPDATE_USER_PHONE_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    try {
      const rows = await this.db
        .update(user)
        .set({
          phone: input.phone,
          phoneVerified: input.phoneVerified,
          updatedAt: new Date(),
        })
        .where(eq(user.id, input.userId))
        .returning({ id: user.id });

      if (rows.length === 0) return D2Result.notFound();

      return D2Result.ok({ data: {} });
    } catch (err: unknown) {
      if (typeof err === "object" && err !== null && "code" in err && err.code === "23505") {
        return D2Result.conflict();
      }
      throw err;
    }
  }
}
