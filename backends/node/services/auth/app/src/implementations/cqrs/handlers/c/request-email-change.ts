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
  ICheckEmailAvailabilityHandler,
  IGetUserByIdHandler,
  IUpdateUserEmailHandler,
} from "../../../../interfaces/repository/handlers/index.js";

type Input = Commands.RequestEmailChangeInput;
type Output = Commands.RequestEmailChangeOutput;

const schema = z.object({
  userId: zodGuid,
  newEmail: z.string().email().max(254),
  currentPassword: z.string().min(1).max(256),
});

/**
 * Initiates an email change. Validates the user's current password atomically
 * (same request body), checks the new email is available and different from
 * current, generates a 6-digit OTP, stores it in the verification table, and
 * sends it via Comms to the NEW email address using `alternativeContactInfo`
 * (the new email isn't a Geo contact yet).
 *
 * Sequence:
 *  1. Validate input (Zod)
 *  2. Verify currentPassword — wrong: 401, abort, NO state changes
 *  3. Check rate limit
 *  4. Check newEmail not in use, not same as current
 *  5. Generate code, hash it
 *  6. Delete any prior pending record (replace semantics)
 *  7. Insert verification record (15min TTL)
 *  8. Record send (rate-limit counter + cooldown)
 *  9. Send OTP via Comms (channels: ["email"], alternativeContactInfo)
 *
 * IDOR prevention: userId is injected from IRequestContext, never from input body.
 */
export class RequestEmailChange
  extends BaseHandler<Input, Output>
  implements Commands.IRequestEmailChangeHandler
{
  constructor(
    private readonly passwordVerifier: IVerifyUserPassword,
    private readonly otpRateLimit: IOtpRateLimitStore,
    private readonly verificationStore: IVerificationStore,
    private readonly checkEmailAvailability: ICheckEmailAvailabilityHandler,
    private readonly updateUserEmail: IUpdateUserEmailHandler,
    private readonly getUserById: IGetUserByIdHandler,
    private readonly notify: INotifyHandler,
    private readonly translator: Translator,
    context: IHandlerContext,
  ) {
    super(context);
    void this.updateUserEmail; // intentionally unused here — kept for symmetry / future use
  }

  override get redaction() {
    return Commands.REQUEST_EMAIL_CHANGE_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    // 1. Password gate — atomic with the request, NO bypass possible.
    const passwordOk = await this.passwordVerifier.verify(input.userId, input.currentPassword);
    if (!passwordOk) {
      return D2Result.unauthorized({ messages: [TK.common.errors.UNAUTHORIZED] });
    }

    // 2. Rate limit — does not count password failures toward OTP budget.
    const cooldown = await this.otpRateLimit.getCooldownSeconds(input.userId, "email");
    if (cooldown > 0) {
      return D2Result.tooManyRequests({
        messages: [TK.common.errors.TOO_MANY_REQUESTS],
        errorCode: "OTP_RATE_LIMITED",
      });
    }

    // 3. New email must not be in use by another user.
    const availability = await this.checkEmailAvailability.handleAsync({
      email: input.newEmail,
    });
    if (!availability.success) return D2Result.bubbleFail(availability);
    if (!availability.data?.available) {
      return D2Result.conflict({ messages: [TK.auth.errors.EMAIL_ALREADY_TAKEN] });
    }

    // 4. Generate code + persist pending record (replaces any prior pending).
    const code = generateOtpCode();
    const codeHash = hashOtpCode(code);
    const identifier = pendingChangeIdentifier("email", input.userId);
    const expiresAt = new Date(Date.now() + OTP_EXPIRY.EMAIL_MS);

    const existing = await this.verificationStore.findByIdentifier(identifier);
    if (existing) {
      await this.verificationStore.deleteById(existing.id);
    }
    await this.verificationStore.create({
      identifier,
      value: encodePendingValue({ codeHash, pendingValue: input.newEmail, attempts: 0 }),
      expiresAt,
    });

    // 5. Record send (sets cooldown for the next call).
    await this.otpRateLimit.recordSend(input.userId, "email");

    // 6. Send OTP via Comms (transient address — new email not yet a contact).
    const expiryMinutes = String(Math.ceil(OTP_EXPIRY.EMAIL_MS / 60_000));
    const userResult = await this.getUserById.handleAsync({ userId: input.userId });
    const userLocale = resolveLocale(
      userResult.success ? (userResult.data?.user.locale ?? undefined) : undefined,
    );
    const t = this.translator.t;
    const notifyResult = await this.notify.handleAsync({
      alternativeContactInfo: { email: input.newEmail },
      channels: ["email"],
      title: t(userLocale, TK.auth.otp.email.subject),
      content: [
        t(userLocale, TK.auth.otp.email.body, { code }),
        "",
        t(userLocale, TK.auth.otp.email.expiry, { minutes: expiryMinutes }),
        "",
        t(userLocale, TK.auth.otp.email.disclaimer),
      ].join("\n"),
      plaintext: t(userLocale, TK.auth.otp.email.plaintext, { code, minutes: expiryMinutes }),
      correlationId: crypto.randomUUID(),
      senderService: "auth",
    });
    if (!notifyResult.success) {
      // OTP couldn't be delivered — clean up the verification record so user can retry.
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
  RequestEmailChangeInput,
  RequestEmailChangeOutput,
} from "../../../../interfaces/cqrs/handlers/c/request-email-change.js";
