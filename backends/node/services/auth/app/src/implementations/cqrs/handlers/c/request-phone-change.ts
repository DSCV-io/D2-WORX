import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK, type Translator, resolveLocale } from "@d2/i18n";
import {
  generateOtpCode,
  hashOtpCode,
  pendingChangeIdentifier,
  encodePendingValue,
  OTP_EXPIRY,
} from "@d2/auth-domain";
import type { INotifyHandler } from "@d2/comms-client";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { IVerifyUserPassword } from "../../../../interfaces/repository/password-verifier.js";
import type { IOtpRateLimitStore } from "../../../../interfaces/repository/otp-rate-limit-store.js";
import type { IVerificationStore } from "../../../../interfaces/repository/verification-store.js";
import type {
  ICheckPhoneAvailabilityHandler,
  IGetUserByIdHandler,
} from "../../../../interfaces/repository/handlers/index.js";

type Input = Commands.RequestPhoneChangeInput;
type Output = Commands.RequestPhoneChangeOutput;

const schema = z.object({
  userId: zodGuid,
  newPhone: z.string().regex(/^\d{7,15}$/, "Phone must be 7-15 digits, no formatting characters"),
  currentPassword: z.string().min(1).max(256),
});

/**
 * Initiates a phone change (or add). Mirrors RequestEmailChange but channel
 * is SMS and TTL is shorter (5min). Phone format is digits-only E.164 (no `+`).
 */
export class RequestPhoneChange
  extends BaseHandler<Input, Output>
  implements Commands.IRequestPhoneChangeHandler
{
  constructor(
    private readonly passwordVerifier: IVerifyUserPassword,
    private readonly otpRateLimit: IOtpRateLimitStore,
    private readonly verificationStore: IVerificationStore,
    private readonly checkPhoneAvailability: ICheckPhoneAvailabilityHandler,
    private readonly getUserById: IGetUserByIdHandler,
    private readonly notify: INotifyHandler,
    private readonly translator: Translator,
    context: IHandlerContext,
  ) {
    super(context);
  }

  override get redaction() {
    return Commands.REQUEST_PHONE_CHANGE_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    // 1. Password gate.
    const passwordOk = await this.passwordVerifier.verify(input.userId, input.currentPassword);
    if (!passwordOk) {
      return D2Result.unauthorized({ messages: [TK.common.errors.UNAUTHORIZED] });
    }

    // 2. Rate limit.
    const cooldown = await this.otpRateLimit.getCooldownSeconds(input.userId, "phone");
    if (cooldown > 0) {
      return D2Result.tooManyRequests({
        messages: [TK.common.errors.TOO_MANY_REQUESTS],
        errorCode: "OTP_RATE_LIMITED",
      });
    }

    // 3. New phone must not already be the user's current phone, and must not
    //    be in use by another user.
    const userResult = await this.getUserById.handleAsync({ userId: input.userId });
    if (userResult.success && userResult.data?.user.phone === input.newPhone) {
      return D2Result.validationFailed({
        messages: [TK.common.errors.BAD_REQUEST],
        errorCode: "PHONE_NO_CHANGE",
      });
    }

    const availability = await this.checkPhoneAvailability.handleAsync({
      phone: input.newPhone,
      excludeUserId: input.userId,
    });
    if (!availability.success) return D2Result.bubbleFail(availability);
    if (!availability.data?.available) {
      return D2Result.conflict({ messages: [TK.common.errors.CONFLICT] });
    }

    // 4. Generate code + persist pending record.
    const code = generateOtpCode();
    const codeHash = hashOtpCode(code);
    const identifier = pendingChangeIdentifier("phone", input.userId);
    const expiresAt = new Date(Date.now() + OTP_EXPIRY.SMS_MS);

    const existing = await this.verificationStore.findByIdentifier(identifier);
    if (existing) {
      await this.verificationStore.deleteById(existing.id);
    }
    await this.verificationStore.create({
      identifier,
      value: encodePendingValue({ codeHash, pendingValue: input.newPhone, attempts: 0 }),
      expiresAt,
    });

    // 5. Record send.
    await this.otpRateLimit.recordSend(input.userId, "phone");

    // 6. Send via SMS.
    const expiryMinutes = String(Math.ceil(OTP_EXPIRY.SMS_MS / 60_000));
    const userLocale = resolveLocale(
      userResult.success ? (userResult.data?.user.locale ?? undefined) : undefined,
    );
    const t = this.translator.t;
    const notifyResult = await this.notify.handleAsync({
      alternativeContactInfo: { phone: input.newPhone },
      channels: ["sms"],
      title: t(userLocale, TK.auth.otp.sms.subject),
      content: t(userLocale, TK.auth.otp.sms.body, { code, minutes: expiryMinutes }),
      plaintext: t(userLocale, TK.auth.otp.sms.plaintext, { code, minutes: expiryMinutes }),
      correlationId: crypto.randomUUID(),
      senderService: "auth",
    });
    if (!notifyResult.success) {
      const stranded = await this.verificationStore.findByIdentifier(identifier);
      if (stranded) await this.verificationStore.deleteById(stranded.id);
      return D2Result.serviceUnavailable({
        messages: [TK.common.errors.SERVICE_UNAVAILABLE],
      });
    }

    return D2Result.ok({ data: { expiresAt } });
  }
}

export type {
  RequestPhoneChangeInput,
  RequestPhoneChangeOutput,
} from "../../../../interfaces/cqrs/handlers/c/request-phone-change.js";
