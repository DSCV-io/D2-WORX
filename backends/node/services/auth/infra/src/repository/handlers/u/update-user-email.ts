import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  UpdateUserEmailInput as I,
  UpdateUserEmailOutput as O,
  IUpdateUserEmailHandler as IHandler,
} from "@d2/auth-app";
import { user } from "../../schema/better-auth-tables.js";

/**
 * Updates a user's email + emailVerified flag atomically.
 *
 * Used by VerifyEmailChange after OTP confirmation. Callers should validate
 * email uniqueness BEFORE calling this handler — this handler will return a
 * PG constraint error if the new email collides with another user's email.
 */
export class UpdateUserEmail extends BaseHandler<I, O> implements IHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    try {
      const rows = await this.db
        .update(user)
        .set({ email: input.email, emailVerified: input.emailVerified, updatedAt: new Date() })
        .where(eq(user.id, input.userId))
        .returning({ id: user.id });

      if (rows.length === 0) return D2Result.notFound();

      return D2Result.ok({ data: {} });
    } catch (err: unknown) {
      // PG unique violation (email collision) → 409
      if (typeof err === "object" && err !== null && "code" in err && err.code === "23505") {
        return D2Result.conflict();
      }
      throw err;
    }
  }
}
