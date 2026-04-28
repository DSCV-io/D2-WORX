import { z } from "zod";
import { BaseHandler, type IHandlerContext, type RedactionSpec, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import { GEO_CONTEXT_KEYS } from "@d2/auth-domain";
import type { ContactToCreateDTO } from "@d2/protos";
import type { Complex, Queries as GeoQueries } from "@d2/geo-client";
import type { IUpdateUserTimezoneHandler as IUpdateUserTimezoneRepoHandler } from "../../../../interfaces/repository/handlers/index.js";
import type { IPushUserUpdated } from "../../../../interfaces/realtime/handlers/index.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { IInvalidateUserSessionCacheHandler } from "../../../../interfaces/cqrs/handlers/c/invalidate-user-session-cache.js";
import { runCrossServiceUpdate } from "../x/cross-service-update.js";

type Input = Commands.UpdateUserTimezoneInput;
type Output = Commands.UpdateUserTimezoneOutput;

const schema = z.object({
  userId: zodGuid,
  timezone: z.string().min(1).max(64),
});

/** Redaction spec — timezone is not PII but handler touches Geo contacts internally. */
export const UPDATE_USER_TIMEZONE_REDACTION: RedactionSpec = {};

/**
 * Updates a user's timezone preference via SAGA pattern.
 *
 * Coordinates two updates (mirrors UpdateUserLocale):
 * 1. Geo contact (ianaIdentifier) via UpdateContactsByExtKeys
 * 2. BetterAuth user.timezone via repo handler
 *
 * On auth failure after Geo succeeded → Geo is rolled back to the original
 * timezone value. On rollback failure → logger.fatal() (CRITICAL).
 *
 * IDOR prevention: userId is injected from IRequestContext, never from input.
 */
export class UpdateUserTimezone
  extends BaseHandler<Input, Output>
  implements Commands.IUpdateUserTimezoneHandler
{
  override get redaction() {
    return UPDATE_USER_TIMEZONE_REDACTION;
  }

  private readonly getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler;
  private readonly updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler;
  private readonly updateUserTimezoneRepo: IUpdateUserTimezoneRepoHandler;
  private readonly pushUserUpdated?: IPushUserUpdated;
  private readonly invalidateSessionCache?: IInvalidateUserSessionCacheHandler;

  constructor(
    getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler,
    updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler,
    updateUserTimezoneRepo: IUpdateUserTimezoneRepoHandler,
    context: IHandlerContext,
    pushUserUpdated?: IPushUserUpdated,
    invalidateSessionCache?: IInvalidateUserSessionCacheHandler,
  ) {
    super(context);
    this.getContactsByExtKeys = getContactsByExtKeys;
    this.updateContactsByExtKeys = updateContactsByExtKeys;
    this.updateUserTimezoneRepo = updateUserTimezoneRepo;
    this.pushUserUpdated = pushUserUpdated;
    this.invalidateSessionCache = invalidateSessionCache;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const extKey = { contextKey: GEO_CONTEXT_KEYS.USER, relatedEntityId: input.userId };

    // Fetch existing contact — needed both for the merge AND saga rollback target.
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

    const oldContact: ContactToCreateDTO = {
      ...existingFields,
      createdAt: new Date(),
      contextKey: extKey.contextKey,
      relatedEntityId: extKey.relatedEntityId,
    };

    const newContact: ContactToCreateDTO = {
      ...existingFields,
      createdAt: new Date(),
      contextKey: extKey.contextKey,
      relatedEntityId: extKey.relatedEntityId,
      ianaIdentifier: input.timezone,
    };

    const result = await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: this.updateContactsByExtKeys,
      operationLabel: "user.timezone",
      context: this.context,
      authUpdate: () =>
        this.updateUserTimezoneRepo.handleAsync({
          userId: input.userId,
          timezone: input.timezone,
        }),
    });
    if (!result.success) return D2Result.bubbleFail(result);

    // Invalidate session cache so next SSR load reads fresh timezone
    await this.invalidateSessionCache?.handleAsync({ userId: input.userId }).catch(() => {});
    await this.pushUserUpdated?.handleAsync({ userId: input.userId }).catch(() => {});

    return D2Result.ok({ data: {} });
  }
}

export type {
  UpdateUserTimezoneInput,
  UpdateUserTimezoneOutput,
} from "../../../../interfaces/cqrs/handlers/c/update-user-timezone.js";
