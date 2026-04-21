import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK, type Translator, resolveLocale } from "@d2/i18n";
import { AUTH_MESSAGING } from "@d2/auth-domain";
import type { INotifyHandler } from "@d2/comms-client";
import type { IMessagePublisher } from "@d2/messaging";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { IAnonymizeUserHandler } from "../../../../interfaces/repository/handlers/u/anonymize-user.js";

type Input = Commands.FinalizeDeletedUserInput;
type Output = Commands.FinalizeDeletedUserOutput;

/**
 * Per-user worker called concurrently from `CleanupDeletedUsers` after the
 * grace cutoff has passed. Owns the post-grace tear-down for ONE user:
 *
 *   1. AnonymizeUser (single transaction with `WHERE status='pending_deletion'`
 *      guard). If the guard misses (user signed back in between find and
 *      finalize, or another worker raced us), returns `{ anonymized: false }`
 *      and we no-op the rest.
 *   2. Send the final "deletion complete" email via `alternativeContactInfo`
 *      using the original email captured BEFORE the scrub (Geo will tear
 *      down the contact when it consumes the user-anonymize event, so we
 *      can't route through the contact pipeline anymore).
 *   3. Publish `auth.user-anonymize` (fanout). Geo / Comms / Files each
 *      subscribe independently and anonymize their own refs. No completion
 *      tracking — downstream services own their idempotency.
 *
 * Email + publish are best-effort (logged on failure) — the DB anonymization
 * is the real business outcome and has already committed by this point.
 */
export class FinalizeDeletedUser
  extends BaseHandler<Input, Output>
  implements Commands.IFinalizeDeletedUserHandler
{
  constructor(
    private readonly anonymizeUser: IAnonymizeUserHandler,
    private readonly notify: INotifyHandler,
    private readonly translator: Translator,
    context: IHandlerContext,
    private readonly publisher?: IMessagePublisher,
  ) {
    super(context);
  }

  override get redaction() {
    return Commands.FINALIZE_DELETED_USER_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const result = await this.anonymizeUser.handleAsync({ userId: input.userId });
    if (!result.success) return D2Result.bubbleFail(result);

    const data = result.data;
    if (!data?.anonymized) {
      return D2Result.ok({ data: { anonymized: false } });
    }

    const originalEmail = data.originalEmail;
    const originalName = data.originalName ?? "";
    const anonymizedAt = new Date().toISOString();

    // 2. Final "deletion complete" email — original email + name captured
    //    pre-scrub. Best-effort: failure here doesn't undo the DB tx.
    if (originalEmail) {
      const t = this.translator.t;
      const userLocale = resolveLocale(undefined);
      this.notify
        .handleAsync({
          alternativeContactInfo: { email: originalEmail },
          channels: ["email"],
          title: t(userLocale, TK.auth.email.userDeletionComplete.subject),
          content: t(userLocale, TK.auth.email.userDeletionComplete.body, { name: originalName }),
          plaintext: t(userLocale, TK.auth.email.userDeletionComplete.plaintext, {
            name: originalName,
          }),
          correlationId: crypto.randomUUID(),
          senderService: "auth",
        })
        .catch((err: unknown) => {
          this.context.logger.warn(
            "FinalizeDeletedUser: complete-email notify failed (non-critical)",
            { error: err instanceof Error ? err.message : String(err) },
          );
        });
    }

    // 3. Publish fanout — downstream services (Geo / Comms / Files) each
    //    subscribe independently. Routing key is empty for fanout exchanges.
    if (this.publisher) {
      this.publisher
        .send(
          { exchange: AUTH_MESSAGING.USER_ANONYMIZE_EXCHANGE, routingKey: "" },
          { userId: input.userId, email: originalEmail, name: originalName, anonymizedAt },
        )
        .catch((err: unknown) => {
          this.context.logger.warn(
            "FinalizeDeletedUser: user-anonymize publish failed (non-critical)",
            { error: err instanceof Error ? err.message : String(err) },
          );
        });
    }

    return D2Result.ok({ data: { anonymized: true } });
  }
}

export type {
  FinalizeDeletedUserInput,
  FinalizeDeletedUserOutput,
} from "../../../../interfaces/cqrs/handlers/c/finalize-deleted-user.js";
