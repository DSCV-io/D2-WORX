import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK, type Translator, resolveLocale } from "@d2/i18n";
import {
  decodePendingValue,
  encodePendingValue,
  GEO_CONTEXT_KEYS,
  hashOtpCode,
  OTP_VERIFY,
  pendingChangeIdentifier,
} from "@d2/auth-domain";
import type { ContactToCreateDTO } from "@d2/protos";
import type { Complex, Queries as GeoQueries } from "@d2/geo-client";
import type { INotifyHandler } from "@d2/comms-client";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { IOtpRateLimitStore } from "../../../../interfaces/repository/otp-rate-limit-store.js";
import type { IVerificationStore } from "../../../../interfaces/repository/verification-store.js";
import type {
  IGetUserByIdHandler,
  IUpdateUserEmailHandler,
} from "../../../../interfaces/repository/handlers/index.js";
import type { IPushUserUpdated } from "../../../../interfaces/realtime/handlers/index.js";
import type { IInvalidateUserSessionCacheHandler } from "../../../../interfaces/cqrs/handlers/c/invalidate-user-session-cache.js";
import { runCrossServiceUpdate } from "../u/cross-service-update.js";

type Input = Commands.VerifyEmailChangeInput;
type Output = Commands.VerifyEmailChangeOutput;

const schema = z.object({
  userId: zodGuid,
  code: z.string().regex(/^\d{6}$/, "Invalid code"),
});

/**
 * Verifies the OTP code from RequestEmailChange and applies the email change
 * via SAGA pattern (Geo first → Auth second → compensate Geo on auth failure).
 *
 * On success: deletes the verification record, clears the rate limit, sends
 * a security notification to the OLD email address ("Your email was changed").
 *
 * On wrong code: increments attempts; deletes record at MAX_ATTEMPTS.
 */
export class VerifyEmailChange
  extends BaseHandler<Input, Output>
  implements Commands.IVerifyEmailChangeHandler
{
  constructor(
    private readonly verificationStore: IVerificationStore,
    private readonly otpRateLimit: IOtpRateLimitStore,
    private readonly updateUserEmailRepo: IUpdateUserEmailHandler,
    private readonly getUserById: IGetUserByIdHandler,
    private readonly getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler,
    private readonly updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler,
    private readonly notify: INotifyHandler,
    private readonly translator: Translator,
    context: IHandlerContext,
    private readonly pushUserUpdated?: IPushUserUpdated,
    private readonly invalidateSessionCache?: IInvalidateUserSessionCacheHandler,
  ) {
    super(context);
  }

  override get redaction() {
    return Commands.VERIFY_EMAIL_CHANGE_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const identifier = pendingChangeIdentifier("email", input.userId);
    const record = await this.verificationStore.findByIdentifier(identifier);

    if (!record) {
      return D2Result.notFound({ messages: [TK.common.errors.NOT_FOUND] });
    }

    // Expired? Delete + return notFound (forces user to request a fresh code).
    if (record.expiresAt.getTime() < Date.now()) {
      await this.verificationStore.deleteById(record.id);
      return D2Result.notFound({ messages: [TK.common.errors.NOT_FOUND] });
    }

    const decoded = decodePendingValue(record.value);
    if (!decoded) {
      // Malformed value — corrupt record. Burn it.
      await this.verificationStore.deleteById(record.id);
      return D2Result.notFound({ messages: [TK.common.errors.NOT_FOUND] });
    }

    if (decoded.attempts >= OTP_VERIFY.MAX_ATTEMPTS) {
      await this.verificationStore.deleteById(record.id);
      return D2Result.tooManyRequests({
        messages: [TK.common.errors.TOO_MANY_REQUESTS],
        errorCode: "OTP_MAX_ATTEMPTS",
      });
    }

    if (hashOtpCode(input.code) !== decoded.codeHash) {
      // Wrong code — increment attempts. Burn at max.
      const newAttempts = decoded.attempts + 1;
      if (newAttempts >= OTP_VERIFY.MAX_ATTEMPTS) {
        await this.verificationStore.deleteById(record.id);
      } else {
        await this.verificationStore.updateValue(
          record.id,
          encodePendingValue({ ...decoded, attempts: newAttempts }),
        );
      }
      return D2Result.unauthorized({ messages: [TK.common.errors.UNAUTHORIZED] });
    }

    // Code is correct — capture old email + locale, then run SAGA: Geo first, then auth.
    const userResult = await this.getUserById.handleAsync({ userId: input.userId });
    const oldEmail = userResult.success ? userResult.data?.user.email : undefined;
    const userLocale = resolveLocale(userResult.success ? userResult.data?.user.locale : undefined);
    const newEmail = decoded.pendingValue;

    const extKey = { contextKey: GEO_CONTEXT_KEYS.USER, relatedEntityId: input.userId };
    const existingResult = await this.getContactsByExtKeys.handleAsync({ keys: [extKey] });
    if (!existingResult.success) {
      return D2Result.serviceUnavailable({ messages: [TK.common.errors.SERVICE_UNAVAILABLE] });
    }
    const mapKey = `${extKey.contextKey}:${extKey.relatedEntityId}`;
    const existingContact = existingResult.data?.data.get(mapKey)?.[0];
    const { id: _, ...existingFields } = existingContact ?? {};

    const oldContact: ContactToCreateDTO = {
      ...existingFields,
      createdAt: new Date(),
      contextKey: extKey.contextKey,
      relatedEntityId: extKey.relatedEntityId,
    };

    // Mirror the new email into the contact's first email entry.
    const existingEmails = existingContact?.contactMethods?.emails ?? [];
    const newEmails =
      existingEmails.length > 0
        ? [{ ...existingEmails[0], value: newEmail }, ...existingEmails.slice(1)]
        : [{ value: newEmail, labels: [] }];

    const newContact: ContactToCreateDTO = {
      ...existingFields,
      createdAt: new Date(),
      contextKey: extKey.contextKey,
      relatedEntityId: extKey.relatedEntityId,
      contactMethods: {
        ...(existingContact?.contactMethods ?? { phoneNumbers: [], emails: [] }),
        emails: newEmails,
      },
    };

    const sagaResult = await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: this.updateContactsByExtKeys,
      operationLabel: "user.email",
      context: this.context,
      authUpdate: () =>
        this.updateUserEmailRepo.handleAsync({
          userId: input.userId,
          email: newEmail,
          emailVerified: true,
        }),
    });
    if (!sagaResult.success) return D2Result.bubbleFail(sagaResult);

    // Both services consistent — clean up.
    await this.verificationStore.deleteById(record.id);
    await this.otpRateLimit.clearOnSuccess(input.userId, "email");

    // Security notification to OLD email (best-effort — doesn't fail the request).
    if (oldEmail && oldEmail !== newEmail) {
      const t = this.translator.t;
      this.notify
        .handleAsync({
          alternativeContactInfo: { email: oldEmail },
          channels: ["email"],
          title: t(userLocale, TK.auth.email.emailChanged.subject),
          content: t(userLocale, TK.auth.email.emailChanged.body, { newEmail }),
          plaintext: t(userLocale, TK.auth.email.emailChanged.plaintext, { newEmail }),
          correlationId: crypto.randomUUID(),
          senderService: "auth",
        })
        .catch((err: unknown) => {
          this.context.logger.warn("Email-changed notification failed (non-critical)", {
            error: err instanceof Error ? err.message : String(err),
          });
        });
    }

    // Real-time refresh — best-effort.
    await this.invalidateSessionCache?.handleAsync({ userId: input.userId }).catch(() => {});
    await this.pushUserUpdated?.handleAsync({ userId: input.userId }).catch(() => {});

    return D2Result.ok({ data: { newEmail } });
  }
}

export type {
  VerifyEmailChangeInput,
  VerifyEmailChangeOutput,
} from "../../../../interfaces/cqrs/handlers/c/verify-email-change.js";
