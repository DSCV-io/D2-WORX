import { eq, and } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import { USER_STATUS } from "@d2/auth-domain";
import {
  ANONYMIZE_USER_REDACTION,
  type AnonymizeUserInput as I,
  type AnonymizeUserOutput as O,
  type IAnonymizeUserHandler,
} from "@d2/auth-app";
import { user, account, session } from "../../schema/better-auth-tables.js";
import { signInEvent } from "../../schema/custom-tables.js";

/**
 * Sentinel value written to non-nullable PII columns on sign_in_event rows
 * during anonymization. Both `ip_address` and `user_agent` are NOT NULL
 * (they're required at sign-in time); we replace rather than NULL so the
 * column constraint stays meaningful for active rows. Anyone reading an
 * anonymized row sees the sentinel and knows the data was scrubbed.
 */
const ANONYMIZED_PLACEHOLDER = "[anonymized]";

/**
 * Anonymizes a user at end of deletion grace.
 *
 * Single transaction so the row is either fully tombstoned or fully untouched
 * — partial anonymization would leave PII behind. Status guard re-checks
 * `pending_deletion` inside the tx so a concurrent sign-in (which flips
 * status back to `active` via the session.create.before hook) wins the race
 * and the anonymization becomes a silent no-op.
 *
 * Field handling:
 *   - `user.email` → `deleted-{userId}@deleted.local` (keeps unique constraint
 *     happy AND frees the original email for fresh sign-up by a brand new user)
 *   - `user.username` / `displayUsername` → `deleted_{userId}` (also unique-safe)
 *   - `user.name` → "Deleted user" (literal — i18n at display time would
 *     require a separate cosmetic-only column; not worth the complexity here
 *     for an anonymized row that's only seen by admins/auditors)
 *   - `user.image` → null
 *   - `user.phone` → null, `user.phoneVerified` → false
 *   - `user.status` → 'deleted', `user.deletedAt` → NOW (anonymization time)
 *   - **DELETE `account` rows**: this is the critical bit. Provider linkage
 *     (Google sub, credential row, etc.) MUST be released so the same person
 *     can re-register fresh. Cascade FK is `onDelete: cascade` from user, but
 *     we're keeping the user as a tombstone — explicit DELETE.
 *   - **DELETE `session` rows**: housekeeping (sessions were already revoked
 *     when delete was initiated; this just collects the FK refs).
 *   - **Scrub `sign_in_event` rows**: NULL the IP / user agent / device
 *     fingerprint / whoIsId. Keep `successful` + `created_at` for forensic
 *     counts. Don't delete — the user's audit history is referenced by
 *     internal admin tools.
 *   - Keep: `role`, `createdAt`, `status`, `deletedAt`, `deletionFeedback`
 *     (feedback survives — useful for product analytics on a fully-anonymized
 *     record; it's user-supplied free text but anonymized via no userId link).
 */
export class AnonymizeUser extends BaseHandler<I, O> implements IAnonymizeUserHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  get redaction(): RedactionSpec {
    return ANONYMIZE_USER_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    return await this.db.transaction(async (tx) => {
      // Capture the originals BEFORE the scrub. Drizzle's .returning()
      // returns the AFTER values, so a separate SELECT is the only way.
      const [before] = await tx
        .select({
          email: user.email,
          name: user.name,
          status: user.status,
        })
        .from(user)
        .where(eq(user.id, input.userId))
        .limit(1);

      if (!before || before.status !== USER_STATUS.PENDING_DELETION) {
        // Either no such user or someone (sign-in hook) flipped them back to
        // active in the gap between job find + finalize. Silent no-op so the
        // job is fully idempotent and concurrency-safe.
        return D2Result.ok<O>({ data: { anonymized: false } });
      }

      const now = new Date();
      const tombstoneEmail = `deleted-${input.userId}@deleted.local`;
      const tombstoneUsername = `deleted_${input.userId}`;

      // 1. Tombstone the user row + flip status. Guard with status='pending_deletion'
      //    again as belt-and-suspenders — the SELECT above ran in the same tx so
      //    snapshot isolation should already protect us, but the guard makes the
      //    intent explicit and documents the invariant in SQL.
      const updated = await tx
        .update(user)
        .set({
          status: USER_STATUS.DELETED,
          deletedAt: now,
          name: "Deleted user",
          email: tombstoneEmail,
          username: tombstoneUsername,
          displayUsername: tombstoneUsername,
          image: null,
          phone: null,
          phoneVerified: false,
          updatedAt: now,
        })
        .where(and(eq(user.id, input.userId), eq(user.status, USER_STATUS.PENDING_DELETION)))
        .returning({ id: user.id });

      if (updated.length === 0) {
        // Race lost between SELECT and UPDATE — treat as no-op.
        return D2Result.ok<O>({ data: { anonymized: false } });
      }

      // 2. Free the provider linkage (Google sub, credential, etc.) for re-registration.
      await tx.delete(account).where(eq(account.userId, input.userId));

      // 3. Housekeeping: clear sessions (already revoked at initiate; this drops the rows).
      await tx.delete(session).where(eq(session.userId, input.userId));

      // 4. Scrub PII from sign-in audit rows but keep the row for forensic counts.
      // ipAddress + userAgent are NOT NULL — use a sentinel placeholder so the
      // column constraints stay meaningful and a reader can tell at a glance
      // the row's PII was scrubbed. whoIsId + deviceFingerprint are nullable.
      await tx
        .update(signInEvent)
        .set({
          ipAddress: ANONYMIZED_PLACEHOLDER,
          userAgent: ANONYMIZED_PLACEHOLDER,
          deviceFingerprint: null,
          whoIsId: null,
        })
        .where(eq(signInEvent.userId, input.userId));

      return D2Result.ok<O>({
        data: {
          anonymized: true,
          originalEmail: before.email,
          originalName: before.name,
        },
      });
    });
  }
}
