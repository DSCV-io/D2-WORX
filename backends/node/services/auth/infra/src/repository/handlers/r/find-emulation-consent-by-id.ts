import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { EmulationConsent } from "@d2/auth-domain";
import type {
  FindEmulationConsentByIdInput as I,
  FindEmulationConsentByIdOutput as O,
  IFindEmulationConsentByIdHandler,
} from "@d2/auth-app";
import { emulationConsent } from "../../schema/custom-tables.js";

export class FindEmulationConsentById
  extends BaseHandler<I, O>
  implements IFindEmulationConsentByIdHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const [row] = await this.db
      .select()
      .from(emulationConsent)
      .where(eq(emulationConsent.id, input.id));

    if (!row) {
      return D2Result.notFound();
    }

    return D2Result.ok({ data: { consent: toEmulationConsent(row) } });
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
