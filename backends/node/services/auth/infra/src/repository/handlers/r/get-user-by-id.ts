import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import { type UserStatus, USER_STATUS } from "@d2/auth-domain";
import { truthyOrUndefined } from "@d2/utilities";
import type {
  GetUserByIdInput as I,
  GetUserByIdOutput as O,
  IGetUserByIdHandler,
} from "@d2/auth-app";
import { user } from "../../schema/better-auth-tables.js";

export class GetUserById extends BaseHandler<I, O> implements IGetUserByIdHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  get redaction(): RedactionSpec {
    return { suppressOutput: true };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const rows = await this.db
      .select({
        id: user.id,
        email: user.email,
        emailVerified: user.emailVerified,
        name: user.name,
        phone: user.phone,
        phoneVerified: user.phoneVerified,
        locale: user.locale,
        timezone: user.timezone,
        status: user.status,
      })
      .from(user)
      .where(eq(user.id, input.userId))
      .limit(1);

    const row = rows[0];
    if (!row) return D2Result.notFound();

    // DB row → domain: collapse null + empty/whitespace strings to undefined
    // (no empty strings as data — `truthyOrUndefined` handles both cases).
    return D2Result.ok({
      data: {
        user: {
          id: row.id,
          email: row.email,
          emailVerified: row.emailVerified,
          name: truthyOrUndefined(row.name),
          phone: truthyOrUndefined(row.phone),
          phoneVerified: row.phoneVerified,
          locale: truthyOrUndefined(row.locale),
          timezone: truthyOrUndefined(row.timezone),
          status: (row.status as UserStatus) ?? USER_STATUS.ACTIVE,
        },
      },
    });
  }
}
