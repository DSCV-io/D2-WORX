import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { OrgContact } from "@d2/auth-domain";
import {
  FIND_ORG_CONTACT_BY_ID_REDACTION,
  type FindOrgContactByIdInput as I,
  type FindOrgContactByIdOutput as O,
  type IFindOrgContactByIdHandler,
} from "@d2/auth-app";
import { orgContact } from "../../schema/custom-tables.js";

export class FindOrgContactById extends BaseHandler<I, O> implements IFindOrgContactByIdHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return FIND_ORG_CONTACT_BY_ID_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const [row] = await this.db.select().from(orgContact).where(eq(orgContact.id, input.id));

    if (!row) {
      return D2Result.notFound();
    }

    return D2Result.ok({ data: { contact: toOrgContact(row) } });
  }
}

function toOrgContact(row: typeof orgContact.$inferSelect): OrgContact {
  return {
    id: row.id,
    organizationId: row.organizationId,
    label: row.label,
    isPrimary: row.isPrimary,
    createdAt: row.createdAt,
    updatedAt: row.updatedAt,
  };
}
