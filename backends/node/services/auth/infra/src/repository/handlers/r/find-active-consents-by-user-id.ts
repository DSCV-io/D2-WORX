import { eq, desc, isNull, gt, and } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { EmulationConsent } from "@d2/auth-domain";
import type {
  FindActiveConsentsByUserIdInput as I,
  FindActiveConsentsByUserIdOutput as O,
  IFindActiveConsentsByUserIdHandler,
} from "@d2/auth-app";
import { emulationConsent } from "../../schema/custom-tables.js";

export class FindActiveConsentsByUserId
  extends BaseHandler<I, O>
  implements IFindActiveConsentsByUserIdHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const now = new Date();
    const rows = await this.db
      .select()
      .from(emulationConsent)
      .where(
        and(
          eq(emulationConsent.userId, input.userId),
          isNull(emulationConsent.revokedAt),
          gt(emulationConsent.expiresAt, now),
        ),
      )
      .orderBy(desc(emulationConsent.createdAt))
      .limit(input.limit)
      .offset(input.offset);

    return D2Result.ok({ data: { consents: rows.map(toEmulationConsent) } });
  }
}

function toEmulationConsent(row: typeof emulationConsent.$inferSelect): EmulationConsent {
  return {
    id: row.id,
    userId: row.userId,
    grantedToOrgId: row.grantedToOrgId,
    expiresAt: row.expiresAt,
    revokedAt: row.revokedAt ?? undefined,
    createdAt: row.createdAt,
  };
}
