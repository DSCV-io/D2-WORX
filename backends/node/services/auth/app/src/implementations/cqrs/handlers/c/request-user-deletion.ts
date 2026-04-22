import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK, type Translator, resolveLocale } from "@d2/i18n";
import { USER_STATUS, USER_DELETION } from "@d2/auth-domain";
import type { INotifyHandler } from "@d2/comms-client";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { IVerifyUserPassword } from "../../../../interfaces/repository/password-verifier.js";
import type {
  IGetUserByIdHandler,
  IUpdateUserStatusHandler,
  ICheckSoleOwnerOrgsHandler,
  IDeleteAllUserSessionsHandler,
} from "../../../../interfaces/repository/handlers/index.js";
import type { IInvalidateUserSessionCacheHandler } from "../../../../interfaces/cqrs/handlers/c/invalidate-user-session-cache.js";

type Input = Commands.RequestUserDeletionInput;
type Output = Commands.RequestUserDeletionOutput;

/**
 * Formats a Date as a human-readable long-form string in the user's locale +
 * timezone — e.g. "May 22, 2026 at 12:43 PM MDT".
 *
 * Uses individual field options (year/month/day/hour/minute) rather than
 * `dateStyle`/`timeStyle` — those two are mutually exclusive with
 * `timeZoneName` per spec, and combining them silently throws RangeError
 * which would mask the timezone in the rendered email.
 *
 * Falls back to UTC + abbreviation only when the supplied timezone is an
 * invalid IANA identifier (Intl throws RangeError on construct).
 */
function formatDateTimeLong(date: Date, locale: string, timezone?: string): string {
  const options: Intl.DateTimeFormatOptions = {
    year: "numeric",
    month: "long",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    timeZone: timezone,
    timeZoneName: "short",
  };
  try {
    return new Intl.DateTimeFormat(locale, options).format(date);
  } catch {
    return new Intl.DateTimeFormat(locale, { ...options, timeZone: "UTC" }).format(date);
  }
}

const schema = z.object({
  userId: zodGuid,
  currentPassword: z.string().min(1).max(256),
  feedback: z
    .object({
      reason: z.string().max(200).optional(),
      comment: z.string().max(2000).optional(),
    })
    .optional(),
  timezoneOverride: z.string().max(100).optional(),
});

/**
 * Initiates self-service user deletion.
 *
 * Flow (atomic, password-gated):
 *   1. Validate input + verify currentPassword (atomic — same body as the
 *      action; no separate password endpoint to bypass).
 *   2. Block if the user is the sole owner of any org (must transfer
 *      ownership first; org-deletion-by-email-link is a separate ticket).
 *   3. Flip status to `pending_deletion`, set `deleted_at = NOW()`, persist
 *      optional feedback.
 *   4. Hard-delete all session rows (user is logged out everywhere — they
 *      must actively sign back in to cancel during grace).
 *   5. Bust BetterAuth's Redis session cookie cache so other devices can't
 *      ride a stale cookie until the cache TTL expires.
 *   6. Send "scheduled for deletion" email (security-relevant, sent via
 *      `alternativeContactInfo` to bypass channel preferences — mirrors the
 *      `phoneRemoved` pattern).
 *
 * Returns the ISO date when permanent anonymization will run if the user
 * doesn't sign back in. Frontend uses this for the "scheduled" landing page.
 */
export class RequestUserDeletion
  extends BaseHandler<Input, Output>
  implements Commands.IRequestUserDeletionHandler
{
  constructor(
    private readonly passwordVerifier: IVerifyUserPassword,
    private readonly getUserById: IGetUserByIdHandler,
    private readonly checkSoleOwnerOrgs: ICheckSoleOwnerOrgsHandler,
    private readonly updateUserStatus: IUpdateUserStatusHandler,
    private readonly deleteAllUserSessions: IDeleteAllUserSessionsHandler,
    private readonly notify: INotifyHandler,
    private readonly translator: Translator,
    context: IHandlerContext,
    private readonly invalidateSessionCache?: IInvalidateUserSessionCacheHandler,
  ) {
    super(context);
  }

  override get redaction() {
    return Commands.REQUEST_USER_DELETION_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    // 1. Atomic password gate — same body as the action.
    const passwordOk = await this.passwordVerifier.verify(input.userId, input.currentPassword);
    if (!passwordOk) {
      return D2Result.unauthorized({ messages: [TK.common.errors.UNAUTHORIZED] });
    }

    // 2. Sole-owner block. Org deletion via email-confirm flow is a separate ticket;
    //    until then the user has to transfer ownership manually.
    const ownerCheck = await this.checkSoleOwnerOrgs.handleAsync({ userId: input.userId });
    if (!ownerCheck.success) return D2Result.bubbleFail(ownerCheck);
    const blockingOrgIds = ownerCheck.data?.soleOwnerOrgIds ?? [];
    if (blockingOrgIds.length > 0) {
      return D2Result.conflict({
        messages: [TK.auth.errors.SOLE_OWNER_OF_ORGS],
        // Tucked into messages via the standard contract; the route handler can
        // also surface blockingOrgIds in a structured field if/when we extend
        // D2Result.fail to carry custom data. For now the FE can call
        // CheckSoleOwnerOrgs separately if it needs the list to render a link.
      });
    }

    // 3. Capture user info for the email BEFORE the status flip (locale +
    //    email don't change as a result of the flip, but reading once keeps
    //    everything from the same snapshot).
    const userResult = await this.getUserById.handleAsync({ userId: input.userId });
    if (!userResult.success) return D2Result.bubbleFail(userResult);
    const userEmail = userResult.data?.user.email;
    const userName = userResult.data?.user.name ?? "";
    const userLocale = resolveLocale(userResult.data?.user.locale ?? undefined);
    // Prefer the route-supplied override (D2_TIMEZONE cookie — matches the
    // tz the user is currently using to view dates in the UI). Falls back to
    // the persisted user.timezone column, then UTC.
    const userTimezone = input.timezoneOverride ?? userResult.data?.user.timezone ?? undefined;

    // 4. Flip status + set grace clock + persist feedback.
    //    `expectedStatus: ACTIVE` is a defense-in-depth CAS guard — if the
    //    user is already `pending_deletion` (double-click on the button) or
    //    `deleted` (shouldn't happen since they'd be signed out), we no-op
    //    instead of clobbering deletedAt / deletionFeedback.
    const now = new Date();
    const statusResult = await this.updateUserStatus.handleAsync({
      userId: input.userId,
      status: USER_STATUS.PENDING_DELETION,
      deletedAt: now,
      deletionFeedback: (input.feedback as Record<string, unknown> | undefined) ?? null,
      expectedStatus: USER_STATUS.ACTIVE,
    });
    if (!statusResult.success) return D2Result.bubbleFail(statusResult);
    if (!statusResult.data?.updated) {
      // User row not found — should not happen since password verify just succeeded.
      return D2Result.notFound({ messages: [TK.common.errors.NOT_FOUND] });
    }

    // 5. Revoke all sessions everywhere (incl. the current one — caller is
    //    about to be redirected to the public scheduled-deletion landing page).
    await this.deleteAllUserSessions.handleAsync({ userId: input.userId });

    // Bust BetterAuth's Redis session cookie cache so other devices can't
    // ride a stale cookie until the cookieCacheMaxAge expires (5 min).
    // Fire-and-forget — the DB DELETE is the real revocation.
    await this.invalidateSessionCache
      ?.handleAsync({ userId: input.userId })
      .catch((err: unknown) => {
        this.context.logger.warn("RequestUserDeletion: session cache bust failed (non-critical)", {
          error: err instanceof Error ? err.message : String(err),
        });
      });

    // 6. Send "scheduled" email — alternativeContactInfo so security-relevant
    //    notifications bypass channel preferences (mirrors phoneRemoved).
    //    The API caller still receives the raw ISO string (frontend renders
    //    its own localized date on the scheduled-deletion landing page); the
    //    email body gets a pre-formatted human-readable string in the user's
    //    saved locale + timezone so the recipient sees "May 22, 2026, 11:23 AM
    //    MDT" instead of an ISO blob.
    const scheduledForDate = new Date(now.getTime() + USER_DELETION.GRACE_PERIOD_MS);
    const scheduledFor = scheduledForDate.toISOString();
    const scheduledForDisplay = formatDateTimeLong(
      scheduledForDate,
      userLocale,
      userTimezone,
    );
    if (userEmail) {
      const t = this.translator.t;
      this.notify
        .handleAsync({
          alternativeContactInfo: { email: userEmail },
          channels: ["email"],
          title: t(userLocale, TK.auth.email.userDeletionScheduled.subject),
          content: t(userLocale, TK.auth.email.userDeletionScheduled.body, {
            name: userName,
            scheduledFor: scheduledForDisplay,
          }),
          plaintext: t(userLocale, TK.auth.email.userDeletionScheduled.plaintext, {
            name: userName,
            scheduledFor: scheduledForDisplay,
          }),
          correlationId: crypto.randomUUID(),
          senderService: "auth",
        })
        .catch((err: unknown) => {
          this.context.logger.warn(
            "RequestUserDeletion: scheduled-email notify failed (non-critical)",
            { error: err instanceof Error ? err.message : String(err) },
          );
        });
    }

    return D2Result.ok({ data: { scheduledFor } });
  }
}

export type {
  RequestUserDeletionInput,
  RequestUserDeletionOutput,
} from "../../../../interfaces/cqrs/handlers/c/request-user-deletion.js";
