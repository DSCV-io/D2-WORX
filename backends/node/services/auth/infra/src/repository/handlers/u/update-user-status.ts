import { and, eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import {
  UPDATE_USER_STATUS_REDACTION,
  type UpdateUserStatusInput as I,
  type UpdateUserStatusOutput as O,
  type IUpdateUserStatusHandler,
} from "@d2/auth-app";
import { user } from "../../schema/better-auth-tables.js";

/**
 * Updates the lifecycle status of a user (active → pending_deletion → deleted).
 *
 * Used by:
 *   - `RequestUserDeletion` to flip active → pending_deletion (sets deletedAt)
 *   - `CancelUserDeletion` to flip pending_deletion → active (clears deletedAt)
 *
 * Final transition to `deleted` is done by `AnonymizeUser` in a single
 * transaction so the status flip is atomic with the field scrub.
 */
export class UpdateUserStatus extends BaseHandler<I, O> implements IUpdateUserStatusHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return UPDATE_USER_STATUS_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    // Build the patch — only include fields the caller explicitly passed.
    const patch: Record<string, unknown> = { status: input.status, updatedAt: new Date() };
    if (input.deletedAt !== undefined) patch.deletedAt = input.deletedAt;
    if (input.deletionFeedback !== undefined) patch.deletionFeedback = input.deletionFeedback;

    // CAS guard: when `expectedStatus` is set, the UPDATE only matches if the
    // row's CURRENT status equals it. Defends against the cancel-vs-anonymize
    // race — a fire-and-forget CancelUserDeletion triggered after AnonymizeUser
    // has already committed would otherwise resurrect a tombstone row.
    const where =
      input.expectedStatus !== undefined
        ? and(eq(user.id, input.userId), eq(user.status, input.expectedStatus))
        : eq(user.id, input.userId);

    const rows = await this.db
      .update(user)
      .set(patch)
      .where(where)
      .returning({ id: user.id });

    return D2Result.ok({ data: { updated: rows.length > 0 } });
  }
}
