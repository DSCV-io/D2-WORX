import { z } from "zod";
import { BaseHandler, type IHandlerContext, type RedactionSpec, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import { GEO_CONTEXT_KEYS } from "@d2/auth-domain";
import type { ContactToCreateDTO } from "@d2/protos";
import type { Complex, Queries as GeoQueries } from "@d2/geo-client";
import type { IUpdateUserLocaleHandler as IUpdateUserLocaleRepoHandler } from "../../../../interfaces/repository/handlers/index.js";
import type { IPushUserUpdated } from "../../../../interfaces/realtime/handlers/index.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { IInvalidateUserSessionCacheHandler } from "../../../../interfaces/cqrs/handlers/c/invalidate-user-session-cache.js";
import { runCrossServiceUpdate } from "../x/cross-service-update.js";

type Input = Commands.UpdateUserLocaleInput;
type Output = Commands.UpdateUserLocaleOutput;

/** BCP 47 locale tag pattern: 2-letter language, optional 2-letter region. */
const BCP47_PATTERN = /^[a-z]{2}(-[A-Z]{2})?$/;

const schema = z.object({
  userId: zodGuid,
  locale: z.string().min(2).max(10).regex(BCP47_PATTERN, {
    message: TK.auth.errors.LOCALE_INVALID_FORMAT,
  }),
});

/** Redaction spec — locale is not PII but handler touches Geo contacts internally. */
export const UPDATE_USER_LOCALE_REDACTION: RedactionSpec = {};

/**
 * Updates a user's locale preference via SAGA pattern.
 *
 * Coordinates two updates:
 * 1. Geo contact (ietfBcp47Tag) via UpdateContactsByExtKeys
 * 2. BetterAuth user.locale via repo handler
 *
 * On auth failure after Geo succeeded → Geo is rolled back to the original
 * locale value. On rollback failure → logger.fatal() (CRITICAL).
 *
 * IDOR prevention: userId is injected from IRequestContext, never from input.
 */
export class UpdateUserLocale
  extends BaseHandler<Input, Output>
  implements Commands.IUpdateUserLocaleHandler
{
  override get redaction() {
    return UPDATE_USER_LOCALE_REDACTION;
  }

  private readonly getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler;
  private readonly updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler;
  private readonly updateUserLocaleRepo: IUpdateUserLocaleRepoHandler;
  private readonly pushUserUpdated?: IPushUserUpdated;
  private readonly invalidateSessionCache?: IInvalidateUserSessionCacheHandler;

  constructor(
    getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler,
    updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler,
    updateUserLocaleRepo: IUpdateUserLocaleRepoHandler,
    context: IHandlerContext,
    pushUserUpdated?: IPushUserUpdated,
    invalidateSessionCache?: IInvalidateUserSessionCacheHandler,
  ) {
    super(context);
    this.getContactsByExtKeys = getContactsByExtKeys;
    this.updateContactsByExtKeys = updateContactsByExtKeys;
    this.updateUserLocaleRepo = updateUserLocaleRepo;
    this.pushUserUpdated = pushUserUpdated;
    this.invalidateSessionCache = invalidateSessionCache;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const extKey = { contextKey: GEO_CONTEXT_KEYS.USER, relatedEntityId: input.userId };

    // Fetch existing contact — needed for both the merge AND saga rollback.
    const existingResult = await this.getContactsByExtKeys.handleAsync({ keys: [extKey] });
    if (!existingResult.success) {
      this.context.logger.error("Failed to fetch existing Geo contact for merge", {
        userId: input.userId,
        errorCode: existingResult.errorCode,
      });
      return D2Result.serviceUnavailable({
        messages: [TK.common.errors.SERVICE_UNAVAILABLE],
      });
    }
    const mapKey = `${extKey.contextKey}:${extKey.relatedEntityId}`;
    const existingContact = existingResult.data?.data.get(mapKey)?.[0];
    const { id: _, ...existingFields } = existingContact ?? {};

    // Snapshot — original contact, used as rollback target.
    const oldContact: ContactToCreateDTO = {
      ...existingFields,
      createdAt: new Date(),
      contextKey: extKey.contextKey,
      relatedEntityId: extKey.relatedEntityId,
    };

    // Target — override locale field.
    const newContact: ContactToCreateDTO = {
      ...existingFields,
      createdAt: new Date(),
      contextKey: extKey.contextKey,
      relatedEntityId: extKey.relatedEntityId,
      ietfBcp47Tag: input.locale,
    };

    const result = await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: this.updateContactsByExtKeys,
      operationLabel: "user.locale",
      context: this.context,
      authUpdate: () =>
        this.updateUserLocaleRepo.handleAsync({
          userId: input.userId,
          locale: input.locale,
        }),
    });
    if (!result.success) return D2Result.bubbleFail(result);

    // Locale changes trigger an immediate page reload (Paraglide), so the
    // session cache MUST be fresh before the response returns — otherwise the
    // server load reads stale locale and overrides the cookie back.
    await this.invalidateSessionCache?.handleAsync({ userId: input.userId }).catch(() => {});
    await this.pushUserUpdated?.handleAsync({ userId: input.userId }).catch(() => {});

    return D2Result.ok({ data: {} });
  }
}

export type {
  UpdateUserLocaleInput,
  UpdateUserLocaleOutput,
} from "../../../../interfaces/cqrs/handlers/c/update-user-locale.js";
