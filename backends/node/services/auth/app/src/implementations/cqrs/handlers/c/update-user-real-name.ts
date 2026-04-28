import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid, zodNonEmptyString } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import { cleanDisplayStr } from "@d2/utilities";
import { GEO_CONTEXT_KEYS } from "@d2/auth-domain";
import type { ContactToCreateDTO } from "@d2/protos";
import type { Complex, Queries as GeoQueries } from "@d2/geo-client";
import type { IUpdateUserNameHandler } from "../../../../interfaces/repository/handlers/index.js";
import type { IPushUserUpdated } from "../../../../interfaces/realtime/handlers/index.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { IInvalidateUserSessionCacheHandler } from "../../../../interfaces/cqrs/handlers/c/invalidate-user-session-cache.js";
import { runCrossServiceUpdate } from "../x/cross-service-update.js";

type Input = Commands.UpdateUserRealNameInput;
type Output = Commands.UpdateUserRealNameOutput;

const schema = z.object({
  userId: zodGuid,
  firstName: zodNonEmptyString(255),
  lastName: zodNonEmptyString(255),
});

/**
 * Updates a user's real name (first + last) via SAGA pattern.
 *
 * Coordinates two updates:
 * 1. Geo contact (firstName/lastName) via UpdateContactsByExtKeys
 * 2. BetterAuth user.name (combined "firstName lastName") via repo handler
 *
 * On auth failure after Geo succeeded → Geo is rolled back to the original
 * contact. On rollback failure → logger.fatal() (CRITICAL).
 *
 * IDOR prevention: userId is injected from IRequestContext, never from input.
 */
export class UpdateUserRealName
  extends BaseHandler<Input, Output>
  implements Commands.IUpdateUserRealNameHandler
{
  private readonly getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler;
  private readonly updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler;
  private readonly updateUserNameRepo: IUpdateUserNameHandler;
  private readonly pushUserUpdated?: IPushUserUpdated;
  private readonly invalidateSessionCache?: IInvalidateUserSessionCacheHandler;

  constructor(
    getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler,
    updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler,
    updateUserName: IUpdateUserNameHandler,
    context: IHandlerContext,
    pushUserUpdated?: IPushUserUpdated,
    invalidateSessionCache?: IInvalidateUserSessionCacheHandler,
  ) {
    super(context);
    this.getContactsByExtKeys = getContactsByExtKeys;
    this.updateContactsByExtKeys = updateContactsByExtKeys;
    this.updateUserNameRepo = updateUserName;
    this.pushUserUpdated = pushUserUpdated;
    this.invalidateSessionCache = invalidateSessionCache;
  }

  override get redaction() {
    return Commands.UPDATE_USER_REAL_NAME_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const firstName = cleanDisplayStr(input.firstName);
    if (!firstName) {
      return D2Result.validationFailed({
        inputErrors: [["firstName", TK.auth.errors.FIRST_NAME_REQUIRED]],
      });
    }

    const lastName = cleanDisplayStr(input.lastName);
    if (!lastName) {
      return D2Result.validationFailed({
        inputErrors: [["lastName", TK.auth.errors.LAST_NAME_REQUIRED]],
      });
    }

    const combinedName = `${firstName} ${lastName}`;
    const extKey = { contextKey: GEO_CONTEXT_KEYS.USER, relatedEntityId: input.userId };

    // Fetch existing contact — needed both for the merge AND as the saga rollback target.
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

    // Snapshot of pre-update contact (rollback target if auth fails).
    const oldContact: ContactToCreateDTO = {
      ...existingFields,
      createdAt: new Date(),
      contextKey: extKey.contextKey,
      relatedEntityId: extKey.relatedEntityId,
    };

    // Target state — override personalDetails firstName/lastName.
    const newContact: ContactToCreateDTO = {
      ...existingFields,
      createdAt: new Date(),
      contextKey: extKey.contextKey,
      relatedEntityId: extKey.relatedEntityId,
      personalDetails: {
        ...(existingContact?.personalDetails ?? {}),
        firstName,
        lastName,
      },
    };

    // SAGA: Geo first → Auth second → compensate Geo on auth failure.
    const result = await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: this.updateContactsByExtKeys,
      operationLabel: "user.name",
      context: this.context,
      authUpdate: () =>
        this.updateUserNameRepo.handleAsync({
          userId: input.userId,
          name: combinedName,
        }),
    });
    if (!result.success) return D2Result.bubbleFail(result);

    this.invalidateSessionCache
      ?.handleAsync({ userId: input.userId })
      .then(() => this.pushUserUpdated?.handleAsync({ userId: input.userId }))
      .catch(() => {});

    return D2Result.ok({ data: { name: combinedName } });
  }
}

export type {
  UpdateUserRealNameInput,
  UpdateUserRealNameOutput,
} from "../../../../interfaces/cqrs/handlers/c/update-user-real-name.js";
