import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK, type Translator, resolveLocale } from "@d2/i18n";
import { USER_STATUS } from "@d2/auth-domain";
import type { INotifyHandler } from "@d2/comms-client";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type {
  IGetUserByIdHandler,
  IUpdateUserStatusHandler,
} from "../../../../interfaces/repository/handlers/index.js";

type Input = Commands.CancelUserDeletionInput;
type Output = Commands.CancelUserDeletionOutput;

const schema = z.object({ userId: zodGuid });

/**
 * Cancels a pending user deletion (called from the BetterAuth sign-in hook
 * when an account in the grace window successfully signs back in).
 *
 * Idempotent: a user already `active` (or already fully `deleted`) is a
 * no-op — we return `{ cancelled: false }` and the sign-in proceeds normally.
 *
 * Sends a "deletion cancelled" email via `alternativeContactInfo` so the
 * user has a security audit trail (mirrors the `phoneRemoved` pattern —
 * security-relevant notifications bypass channel preferences).
 */
export class CancelUserDeletion
  extends BaseHandler<Input, Output>
  implements Commands.ICancelUserDeletionHandler
{
  constructor(
    private readonly getUserById: IGetUserByIdHandler,
    private readonly updateUserStatus: IUpdateUserStatusHandler,
    private readonly notify: INotifyHandler,
    private readonly translator: Translator,
    context: IHandlerContext,
  ) {
    super(context);
  }

  override get redaction() {
    return Commands.CANCEL_USER_DELETION_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    // Read the user to (a) confirm status warrants a cancel and (b) capture
    // email + name + locale for the email send. One read serves both.
    const userResult = await this.getUserById.handleAsync({ userId: input.userId });
    if (!userResult.success) return D2Result.bubbleFail(userResult);
    const u = userResult.data?.user;
    if (!u) return D2Result.ok({ data: { cancelled: false } });
    if (u.status !== USER_STATUS.PENDING_DELETION) {
      return D2Result.ok({ data: { cancelled: false } });
    }

    // Flip back to active + clear the grace clock. Leave deletion_feedback
    // in place — it's useful for analytics on cancel-rate ("user reconsidered
    // after submitting reason X").
    //
    // `expectedStatus: PENDING_DELETION` is a CAS guard against the cancel-
    // vs-anonymize race. This handler is invoked fire-and-forget from the
    // BetterAuth sign-in hook; if AnonymizeUser commits between our SELECT
    // above and this UPDATE, an unguarded UPDATE would resurrect a tombstone
    // row (status flipped back to active + DELETED account/session rows
    // already gone — phantom user). The guard makes the cancel a no-op in
    // that race window, which is the right outcome.
    const statusResult = await this.updateUserStatus.handleAsync({
      userId: input.userId,
      status: USER_STATUS.ACTIVE,
      deletedAt: null,
      expectedStatus: USER_STATUS.PENDING_DELETION,
    });
    if (!statusResult.success) return D2Result.bubbleFail(statusResult);
    if (!statusResult.data?.updated) {
      // Guard miss (race lost) OR row vanished between read and update.
      // Either way: treat as no-op — the sign-in hook should not abort the
      // session. If the user is now `deleted`, the sign-in hook itself will
      // throw FORBIDDEN on its own status check.
      return D2Result.ok({ data: { cancelled: false } });
    }

    // Send the cancellation email via alternativeContactInfo (security-
    // relevant: bypass channel preferences, mirrors phoneRemoved).
    const userEmail = u.email;
    const userName = u.name ?? "";
    const userLocale = resolveLocale(u.locale ?? undefined);
    if (userEmail) {
      const t = this.translator.t;
      this.notify
        .handleAsync({
          alternativeContactInfo: { email: userEmail },
          channels: ["email"],
          title: t(userLocale, TK.auth.email.userDeletionCancelled.subject),
          content: t(userLocale, TK.auth.email.userDeletionCancelled.body, { name: userName }),
          plaintext: t(userLocale, TK.auth.email.userDeletionCancelled.plaintext, {
            name: userName,
          }),
          correlationId: crypto.randomUUID(),
          senderService: "auth",
        })
        .catch((err: unknown) => {
          this.context.logger.warn(
            "CancelUserDeletion: cancellation-email notify failed (non-critical)",
            { error: err instanceof Error ? err.message : String(err) },
          );
        });
    }

    return D2Result.ok({ data: { cancelled: true } });
  }
}

export type {
  CancelUserDeletionInput,
  CancelUserDeletionOutput,
} from "../../../../interfaces/cqrs/handlers/c/cancel-user-deletion.js";
