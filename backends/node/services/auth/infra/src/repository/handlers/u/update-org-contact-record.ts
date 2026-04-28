import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import {
  UPDATE_ORG_CONTACT_RECORD_REDACTION,
  type UpdateOrgContactRecordInput as I,
  type UpdateOrgContactRecordOutput as O,
  type IUpdateOrgContactRecordHandler,
} from "@d2/auth-app";
import { orgContact } from "../../schema/custom-tables.js";

export class UpdateOrgContactRecord
  extends BaseHandler<I, O>
  implements IUpdateOrgContactRecordHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return UPDATE_ORG_CONTACT_RECORD_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const rows = await this.db
      .update(orgContact)
      .set({
        label: input.contact.label,
        isPrimary: input.contact.isPrimary,
        updatedAt: input.contact.updatedAt,
      })
      .where(eq(orgContact.id, input.contact.id))
      .returning({ id: orgContact.id });

    if (rows.length === 0) return D2Result.notFound();

    return D2Result.ok({ data: {} });
  }
}
