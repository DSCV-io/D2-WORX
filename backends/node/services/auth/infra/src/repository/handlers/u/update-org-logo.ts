import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import {
  UPDATE_ORG_LOGO_REDACTION,
  type UpdateOrgLogoInput as I,
  type UpdateOrgLogoOutput as O,
  type IUpdateOrgLogoHandler,
} from "@d2/auth-app";
import { organization } from "../../schema/better-auth-tables.js";

export class UpdateOrgLogo extends BaseHandler<I, O> implements IUpdateOrgLogoHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return UPDATE_ORG_LOGO_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const rows = await this.db
      .update(organization)
      .set({ logo: input.logo, updatedAt: new Date() })
      .where(eq(organization.id, input.orgId))
      .returning({ id: organization.id });

    if (rows.length === 0) return D2Result.notFound();

    return D2Result.ok({ data: {} });
  }
}
