import { z } from "zod";
import { BaseHandler, type IHandlerContext, type RedactionSpec, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { GEO_CONTEXT_KEYS } from "@d2/auth-domain";
import type { ContactToCreateDTO } from "@d2/protos";
import type { Complex, Queries as GeoQueries } from "@d2/geo-client";
import type { IUpdateUserLocaleHandler as IUpdateUserLocaleRepoHandler } from "../../../../interfaces/repository/handlers/index.js";
import type { IPushUserUpdated } from "../../../../interfaces/realtime/handlers/index.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { IInvalidateUserSessionCacheHandler } from "../../../../interfaces/cqrs/handlers/c/invalidate-user-session-cache.js";

type Input = Commands.UpdateUserLocaleInput;
type Output = Commands.UpdateUserLocaleOutput;

/** BCP 47 locale tag pattern: 2-letter language, optional 2-letter region. */
const BCP47_PATTERN = /^[a-z]{2}(-[A-Z]{2})?$/;

const schema = z.object({
  userId: zodGuid,
  locale: z.string().min(2).max(10).regex(BCP47_PATTERN, "Invalid locale format"),
});

/** Redaction spec — locale is not PII but handler touches Geo contacts internally. */
export const UPDATE_USER_LOCALE_REDACTION: RedactionSpec = {};

/**
 * Updates a user's locale preference.
 *
 * Coordinates two updates:
 * 1. Geo contact (ietfBcp47Tag) via UpdateContactsByExtKeys
 * 2. BetterAuth user.locale via repo handler
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

    // Fetch existing contact to preserve fields we're not changing.
    const existingResult = await this.getContactsByExtKeys.handleAsync({ keys: [extKey] });
    if (!existingResult.success) {
      this.context.logger.error("Failed to fetch existing Geo contact for merge", {
        userId: input.userId,
        errorCode: existingResult.errorCode,
      });
      return D2Result.serviceUnavailable({
        messages: ["Unable to update locale preference. Please try again."],
      });
    }
    const mapKey = `${extKey.contextKey}:${extKey.relatedEntityId}`;
    const existingContact = existingResult.data?.data.get(mapKey)?.[0];

    // Spread existing contact, override only the locale field.
    const { id: _, ...existingFields } = existingContact ?? {};
    const contactToCreate: ContactToCreateDTO = {
      ...existingFields,
      createdAt: new Date(),
      contextKey: extKey.contextKey,
      relatedEntityId: extKey.relatedEntityId,
      ietfBcp47Tag: input.locale,
    };

    // Cross-service call — do NOT bubbleFail (may leak Geo internals).
    const geoResult = await this.updateContactsByExtKeys.handleAsync({
      contacts: [contactToCreate],
    });
    if (!geoResult.success) {
      this.context.logger.error("Failed to update Geo contact for user locale", {
        userId: input.userId,
        errorCode: geoResult.errorCode,
        statusCode: geoResult.statusCode,
      });
      return D2Result.serviceUnavailable({
        messages: ["Unable to update locale preference. Please try again."],
      });
    }

    // Update BetterAuth user.locale (same-service repo — bubbleFail is safe)
    const localeResult = await this.updateUserLocaleRepo.handleAsync({
      userId: input.userId,
      locale: input.locale,
    });
    if (!localeResult.success) {
      return D2Result.bubbleFail(localeResult);
    }

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
