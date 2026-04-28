import { eq, desc } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { SignInEvent } from "@d2/auth-domain";
import type {
  FindSignInEventsByUserIdInput as I,
  FindSignInEventsByUserIdOutput as O,
  IFindSignInEventsByUserIdHandler,
} from "@d2/auth-app";
import { signInEvent } from "../../schema/custom-tables.js";

export class FindSignInEventsByUserId
  extends BaseHandler<I, O>
  implements IFindSignInEventsByUserIdHandler
{
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
      .select()
      .from(signInEvent)
      .where(eq(signInEvent.userId, input.userId))
      .orderBy(desc(signInEvent.createdAt))
      .limit(input.limit)
      .offset(input.offset);

    return D2Result.ok({ data: { events: rows.map(toSignInEvent) } });
  }
}

function toSignInEvent(row: typeof signInEvent.$inferSelect): SignInEvent {
  return {
    id: row.id,
    userId: row.userId,
    successful: row.successful,
    ipAddress: row.ipAddress,
    userAgent: row.userAgent,
    whoIsId: row.whoIsId ?? undefined,
    deviceFingerprint: row.deviceFingerprint ?? undefined,
    clientFingerprint: row.clientFingerprint ?? undefined,
    serverFingerprint: row.serverFingerprint ?? undefined,
    failureReason: row.failureReason ?? undefined,
    createdAt: row.createdAt,
  };
}
