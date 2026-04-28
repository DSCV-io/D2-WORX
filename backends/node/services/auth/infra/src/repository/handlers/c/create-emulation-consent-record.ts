import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import type {
  CreateEmulationConsentRecordInput as I,
  CreateEmulationConsentRecordOutput as O,
  ICreateEmulationConsentRecordHandler,
} from "@d2/auth-app";
import { emulationConsent } from "../../schema/custom-tables.js";
import { isPgUniqueViolation } from "@d2/errors-pg";

export class CreateEmulationConsentRecord
  extends BaseHandler<I, O>
  implements ICreateEmulationConsentRecordHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    try {
      await this.db.insert(emulationConsent).values({
        id: input.consent.id,
        userId: input.consent.userId,
        grantedToOrgId: input.consent.grantedToOrgId,
        expiresAt: input.consent.expiresAt,
        revokedAt: input.consent.revokedAt ?? null,
        createdAt: input.consent.createdAt,
      });

      return D2Result.ok({ data: {} });
    } catch (err) {
      if (isPgUniqueViolation(err)) {
        return D2Result.conflict({
          messages: [TK.auth.errors.EMULATION_CONSENT_ALREADY_EXISTS],
        });
      }
      throw err;
    }
  }
}
