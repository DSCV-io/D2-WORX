import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  CheckOrgExistsInput as I,
  CheckOrgExistsOutput as O,
  ICheckOrgExistsHandler,
} from "@d2/auth-app";
import { organization } from "../../schema/better-auth-tables.js";

export class CheckOrgExists extends BaseHandler<I, O> implements ICheckOrgExistsHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const rows = await this.db
      .select({ id: organization.id })
      .from(organization)
      .where(eq(organization.id, input.orgId))
      .limit(1);

    return D2Result.ok({ data: { exists: rows.length > 0 } });
  }
}
