import { sql } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type Role } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  CheckSoleOwnerOrgsInput as I,
  CheckSoleOwnerOrgsOutput as O,
  ICheckSoleOwnerOrgsHandler,
} from "@d2/auth-app";

/**
 * `"owner" satisfies Role` is a compile-time guard — if the role enum is
 * ever renamed, this query breaks at build time instead of silently returning
 * an empty set at runtime.
 */
const OWNER_ROLE = "owner" satisfies Role;

/**
 * Returns org IDs where the input user is the SOLE `owner`-role member.
 *
 * Used by `RequestUserDeletion` to block self-deletion when the user owns
 * one or more orgs alone — they must transfer ownership (or delete the org
 * via the email-confirmed flow, which is a separate ticket) before they can
 * delete themselves.
 *
 * Single SQL query with a subquery — one round-trip regardless of how many
 * orgs the user is in. The subquery counts owners per candidate org and
 * filters to those with exactly 1.
 *
 * Uses raw SQL because Drizzle's correlated-subquery support is awkward
 * for this shape. Parameter binding is via `${...}` to keep it injection-safe.
 */
export class CheckSoleOwnerOrgs extends BaseHandler<I, O> implements ICheckSoleOwnerOrgsHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    // Composite index `member_organization_role_idx (organization_id, role)`
    // backs the correlated COUNT(*) so the per-org owner-count stays an
    // index-only scan even on large orgs.
    const result = await this.db.execute<{ organization_id: string }>(sql`
      SELECT m.organization_id
      FROM "member" m
      WHERE m.user_id = ${input.userId}
        AND m.role = ${OWNER_ROLE}
        AND (
          SELECT COUNT(*)
          FROM "member" m2
          WHERE m2.organization_id = m.organization_id
            AND m2.role = ${OWNER_ROLE}
        ) = 1
    `);

    const soleOwnerOrgIds = result.rows.map((r) => r.organization_id);
    return D2Result.ok({ data: { soleOwnerOrgIds } });
  }
}
