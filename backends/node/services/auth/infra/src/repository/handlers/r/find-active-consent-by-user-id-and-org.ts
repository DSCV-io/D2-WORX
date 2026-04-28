import { eq, isNull, gt, and } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { EmulationConsent } from "@d2/auth-domain";
import type {
  FindActiveConsentByUserIdAndOrgInput as I,
  FindActiveConsentByUserIdAndOrgOutput as O,
  IFindActiveConsentByUserIdAndOrgHandler,
} from "@d2/auth-app";
import { emulationConsent } from "../../schema/custom-tables.js";

export class FindActiveConsentByUserIdAndOrg
  extends BaseHandler<I, O>
  implements IFindActiveConsentByUserIdAndOrgHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const now = new Date();
    const [row] = await this.db
      .select()
      .from(emulationConsent)
      .where(
        and(
          eq(emulationConsent.userId, input.userId),
          eq(emulationConsent.grantedToOrgId, input.grantedToOrgId),
          isNull(emulationConsent.revokedAt),
          gt(emulationConsent.expiresAt, now),
        ),
      );

    const consent = row ? toEmulationConsent(row) : undefined;
    return D2Result.ok({ data: { consent } });
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
