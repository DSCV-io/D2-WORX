import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import {
  CREATE_ORG_CONTACT_RECORD_REDACTION,
  type CreateOrgContactRecordInput as I,
  type CreateOrgContactRecordOutput as O,
  type ICreateOrgContactRecordHandler,
} from "@d2/auth-app";
import { orgContact } from "../../schema/custom-tables.js";
import { isPgUniqueViolation } from "@d2/errors-pg";

export class CreateOrgContactRecord
  extends BaseHandler<I, O>
  implements ICreateOrgContactRecordHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return CREATE_ORG_CONTACT_RECORD_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    try {
      await this.db.insert(orgContact).values({
        id: input.contact.id,
        organizationId: input.contact.organizationId,
        label: input.contact.label,
        isPrimary: input.contact.isPrimary,
        createdAt: input.contact.createdAt,
        updatedAt: input.contact.updatedAt,
      });

      return D2Result.ok({ data: {} });
    } catch (err) {
      if (isPgUniqueViolation(err)) {
        return D2Result.conflict();
      }
      throw err;
    }
  }
}
