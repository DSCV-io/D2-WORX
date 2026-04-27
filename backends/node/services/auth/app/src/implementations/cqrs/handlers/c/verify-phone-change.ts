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
  IUpdateUserPhoneHandler,
} from "../../../../interfaces/repository/handlers/index.js";
import type { IPushUserUpdated } from "../../../../interfaces/realtime/handlers/index.js";
import type { IInvalidateUserSessionCacheHandler } from "../../../../interfaces/cqrs/handlers/c/invalidate-user-session-cache.js";
import { runCrossServiceUpdate } from "../u/cross-service-update.js";

type Input = Commands.VerifyPhoneChangeInput;
type Output = Commands.VerifyPhoneChangeOutput;

const schema = z.object({
  userId: zodGuid,
  code: z.string().regex(/^\d{6}$/, "Invalid code"),
});

/**
 * Verifies the OTP from RequestPhoneChange and applies via SAGA.
 * Security notification on success goes to the user's CURRENT email
 * (not the old phone — email is the durable security channel).
 */
export class VerifyPhoneChange
  extends BaseHandler<Input, Output>
  implements Commands.IVerifyPhoneChangeHandler
{
  constructor(
    private readonly verificationStore: IVerificationStore,
    private readonly otpRateLimit: IOtpRateLimitStore,
    private readonly updateUserPhoneRepo: IUpdateUserPhoneHandler,
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
    return Commands.VERIFY_PHONE_CHANGE_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const identifier = pendingChangeIdentifier("phone", input.userId);
    const record = await this.verificationStore.findByIdentifier(identifier);
    if (!record) return D2Result.notFound({ messages: [TK.common.errors.NOT_FOUND] });

    if (record.expiresAt.getTime() < Date.now()) {
      await this.verificationStore.deleteById(record.id);
      return D2Result.notFound({ messages: [TK.common.errors.NOT_FOUND] });
    }

    const decoded = decodePendingValue(record.value);
    if (!decoded) {
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

    // Code correct — fetch user (for current email + phone + locale) and contact (for sync).
    const userResult = await this.getUserById.handleAsync({ userId: input.userId });
    const userEmail = userResult.success ? userResult.data?.user.email : undefined;
    const userLocale = resolveLocale(userResult.success ? userResult.data?.user.locale : undefined);
    const newPhone = decoded.pendingValue;

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

    // Mirror the new phone into the contact's first phone entry.
    const existingPhones = existingContact?.contactMethods?.phoneNumbers ?? [];
    const newPhones =
      existingPhones.length > 0
        ? [{ ...existingPhones[0], value: newPhone }, ...existingPhones.slice(1)]
        : [{ value: newPhone, labels: [] }];

    const newContact: ContactToCreateDTO = {
      ...existingFields,
      createdAt: new Date(),
      contextKey: extKey.contextKey,
      relatedEntityId: extKey.relatedEntityId,
      contactMethods: {
        ...(existingContact?.contactMethods ?? { emails: [], phoneNumbers: [] }),
        phoneNumbers: newPhones,
      },
    };

    const sagaResult = await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: this.updateContactsByExtKeys,
      operationLabel: "user.phone",
      context: this.context,
      authUpdate: () =>
        this.updateUserPhoneRepo.handleAsync({
          userId: input.userId,
          phone: newPhone,
          phoneVerified: true,
          clear: false,
        }),
    });
    if (!sagaResult.success) return D2Result.bubbleFail(sagaResult);

    await this.verificationStore.deleteById(record.id);
    await this.otpRateLimit.clearOnSuccess(input.userId, "phone");

    // Security email to user's current email address (best-effort).
    if (userEmail) {
      const t = this.translator.t;
      this.notify
        .handleAsync({
          alternativeContactInfo: { email: userEmail },
          channels: ["email"],
          title: t(userLocale, TK.auth.email.phoneChanged.subject),
          content: t(userLocale, TK.auth.email.phoneChanged.body),
          plaintext: t(userLocale, TK.auth.email.phoneChanged.plaintext),
          correlationId: crypto.randomUUID(),
          senderService: "auth",
        })
        .catch((err: unknown) => {
          this.context.logger.warn("Phone-changed notification failed (non-critical)", {
            error: err instanceof Error ? err.message : String(err),
          });
        });
    }

    await this.invalidateSessionCache?.handleAsync({ userId: input.userId }).catch(() => {});
    await this.pushUserUpdated?.handleAsync({ userId: input.userId }).catch(() => {});

    return D2Result.ok({ data: { phone: newPhone } });
  }
}

export type {
  VerifyPhoneChangeInput,
  VerifyPhoneChangeOutput,
} from "../../../../interfaces/cqrs/handlers/c/verify-phone-change.js";
