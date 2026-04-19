import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK, type Translator, resolveLocale } from "@d2/i18n";
import { GEO_CONTEXT_KEYS } from "@d2/auth-domain";
import type { ContactToCreateDTO } from "@d2/protos";
import type { Complex, Queries as GeoQueries } from "@d2/geo-client";
import type { INotifyHandler } from "@d2/comms-client";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { IVerifyUserPassword } from "../../../../interfaces/repository/password-verifier.js";
import type {
  IGetUserByIdHandler,
  IUpdateUserPhoneHandler,
} from "../../../../interfaces/repository/handlers/index.js";
import type { IPushUserUpdated } from "../../../../interfaces/realtime/handlers/index.js";
import type { IInvalidateUserSessionCacheHandler } from "../../../../interfaces/cqrs/handlers/c/invalidate-user-session-cache.js";
import { runCrossServiceUpdate } from "../u/cross-service-update.js";

type Input = Commands.RemovePhoneInput;
type Output = Commands.RemovePhoneOutput;

const schema = z.object({
  userId: zodGuid,
  currentPassword: z.string().min(1).max(256),
});

/**
 * Removes the user's phone (sets phone=null, phoneVerified=false).
 * Password gate is the sole defense (no OTP — you're losing functionality,
 * not gaining attack surface). Geo contact's first phone entry is dropped.
 *
 * Idempotent: if phone is already null, returns ok without state changes.
 *
 * Security notification sent to current email after success.
 */
export class RemovePhone
  extends BaseHandler<Input, Output>
  implements Commands.IRemovePhoneHandler
{
  constructor(
    private readonly passwordVerifier: IVerifyUserPassword,
    private readonly getUserById: IGetUserByIdHandler,
    private readonly updateUserPhoneRepo: IUpdateUserPhoneHandler,
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
    return Commands.REMOVE_PHONE_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    // 1. Password gate.
    const passwordOk = await this.passwordVerifier.verify(input.userId, input.currentPassword);
    if (!passwordOk) {
      return D2Result.unauthorized({ messages: [TK.common.errors.UNAUTHORIZED] });
    }

    // 2. Idempotent: no-op if no phone set.
    const userResult = await this.getUserById.handleAsync({ userId: input.userId });
    if (!userResult.success) return D2Result.bubbleFail(userResult);
    const userEmail = userResult.data?.user.email ?? null;
    const oldPhone = userResult.data?.user.phone ?? null;
    const userLocale = resolveLocale(userResult.data?.user.locale ?? undefined);
    if (!oldPhone) {
      return D2Result.ok({ data: {} });
    }

    // 3. Fetch existing contact (rollback target + source for new contact).
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

    // Drop the first phone entry from the contact (mirror — User is the truth).
    const remainingPhones = (existingContact?.contactMethods?.phoneNumbers ?? []).slice(1);
    const newContact: ContactToCreateDTO = {
      ...existingFields,
      createdAt: new Date(),
      contextKey: extKey.contextKey,
      relatedEntityId: extKey.relatedEntityId,
      contactMethods: {
        ...(existingContact?.contactMethods ?? { emails: [], phoneNumbers: [] }),
        phoneNumbers: remainingPhones,
      },
    };

    // 4. SAGA — Geo first → Auth second → compensate Geo on auth failure.
    const sagaResult = await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: this.updateContactsByExtKeys,
      operationLabel: "user.phone (remove)",
      context: this.context,
      authUpdate: () =>
        this.updateUserPhoneRepo.handleAsync({
          userId: input.userId,
          phone: null,
          phoneVerified: false,
        }),
    });
    if (!sagaResult.success) return D2Result.bubbleFail(sagaResult);

    // 5. Security email to current email (best-effort).
    if (userEmail) {
      const t = this.translator.t;
      this.notify
        .handleAsync({
          alternativeContactInfo: { email: userEmail },
          channels: ["email"],
          title: t(userLocale, TK.auth.email.phoneRemoved.subject),
          content: t(userLocale, TK.auth.email.phoneRemoved.body),
          plaintext: t(userLocale, TK.auth.email.phoneRemoved.plaintext),
          correlationId: crypto.randomUUID(),
          senderService: "auth",
        })
        .catch((err: unknown) => {
          this.context.logger.warn("Phone-removed notification failed (non-critical)", {
            error: err instanceof Error ? err.message : String(err),
          });
        });
    }

    await this.invalidateSessionCache?.handleAsync({ userId: input.userId }).catch(() => {});
    await this.pushUserUpdated?.handleAsync({ userId: input.userId }).catch(() => {});

    return D2Result.ok({ data: {} });
  }
}

export type {
  RemovePhoneInput,
  RemovePhoneOutput,
} from "../../../../interfaces/cqrs/handlers/c/remove-phone.js";
