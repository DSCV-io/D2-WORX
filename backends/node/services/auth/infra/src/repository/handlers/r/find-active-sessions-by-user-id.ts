import { eq, and, gt, desc } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { Session, OrgType, Role } from "@d2/auth-domain";
import type {
  FindActiveSessionsByUserIdInput as I,
  FindActiveSessionsByUserIdOutput as O,
  IFindActiveSessionsByUserIdHandler,
} from "@d2/auth-app";
import { session } from "../../schema/better-auth-tables.js";

export class FindActiveSessionsByUserId
  extends BaseHandler<I, O>
  implements IFindActiveSessionsByUserIdHandler
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
    const now = new Date();
    const rows = await this.db
      .select()
      .from(session)
      .where(and(eq(session.userId, input.userId), gt(session.expiresAt, now)))
      .orderBy(desc(session.updatedAt));

    return D2Result.ok({ data: { sessions: rows.map(toSession) } });
  }
}

function toSession(row: typeof session.$inferSelect): Session {
  return {
    id: row.id,
    userId: row.userId,
    token: row.token,
    expiresAt: row.expiresAt,
    ipAddress: row.ipAddress ?? undefined,
    userAgent: row.userAgent ?? undefined,
    createdAt: row.createdAt,
    updatedAt: row.updatedAt,
    activeOrganizationId: row.activeOrganizationId,
    activeOrganizationType: (row.activeOrganizationType as OrgType | null) ?? null,
    activeOrganizationRole: (row.activeOrganizationRole as Role | null) ?? null,
    emulatedOrganizationId: row.emulatedOrganizationId,
    emulatedOrganizationType: (row.emulatedOrganizationType as OrgType | null) ?? null,
    whoIsId: row.whoIsId ?? undefined,
    deviceFingerprint: row.deviceFingerprint ?? undefined,
    clientFingerprint: row.clientFingerprint ?? undefined,
    serverFingerprint: row.serverFingerprint ?? undefined,
  };
}
