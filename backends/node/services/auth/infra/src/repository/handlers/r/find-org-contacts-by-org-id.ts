import { eq, desc, asc } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { OrgContact } from "@d2/auth-domain";
import {
  FIND_ORG_CONTACTS_BY_ORG_ID_REDACTION,
  type FindOrgContactsByOrgIdInput as I,
  type FindOrgContactsByOrgIdOutput as O,
  type IFindOrgContactsByOrgIdHandler,
} from "@d2/auth-app";
import { orgContact } from "../../schema/custom-tables.js";

export class FindOrgContactsByOrgId
  extends BaseHandler<I, O>
  implements IFindOrgContactsByOrgIdHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return FIND_ORG_CONTACTS_BY_ORG_ID_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    let query = this.db
      .select()
      .from(orgContact)
      .where(eq(orgContact.organizationId, input.organizationId))
      .orderBy(desc(orgContact.isPrimary), asc(orgContact.createdAt))
      .$dynamic();

    if (input.limit !== undefined) query = query.limit(input.limit);
    if (input.offset !== undefined) query = query.offset(input.offset);

    const rows = await query;

    return D2Result.ok({ data: { contacts: rows.map(toOrgContact) } });
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
