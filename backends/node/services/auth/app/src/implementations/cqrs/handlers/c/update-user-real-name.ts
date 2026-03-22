import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid, zodNonEmptyString } from "@d2/handler";
import { D2Result } from "@d2/result";
import { cleanDisplayStr } from "@d2/utilities";
import { GEO_CONTEXT_KEYS } from "@d2/auth-domain";
import type { ContactToCreateDTO } from "@d2/protos";
import type { Complex } from "@d2/geo-client";
import type { IUpdateUserNameHandler } from "../../../../interfaces/repository/handlers/index.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Commands.UpdateUserRealNameInput;
type Output = Commands.UpdateUserRealNameOutput;

const schema = z.object({
  userId: zodGuid,
  firstName: zodNonEmptyString(255),
  lastName: zodNonEmptyString(255),
});

/**
 * Updates a user's real name (first + last).
 *
 * Coordinates two updates:
 * 1. Geo contact (firstName/lastName) via UpdateContactsByExtKeys
 * 2. BetterAuth user.name (combined "firstName lastName") via repo handler
 *
 * IDOR prevention: userId is injected from IRequestContext, never from input.
 */
export class UpdateUserRealName
  extends BaseHandler<Input, Output>
  implements Commands.IUpdateUserRealNameHandler
{
  private readonly updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler;
  private readonly updateUserNameRepo: IUpdateUserNameHandler;

  constructor(
    updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler,
    updateUserName: IUpdateUserNameHandler,
    context: IHandlerContext,
  ) {
    super(context);
    this.updateContactsByExtKeys = updateContactsByExtKeys;
    this.updateUserNameRepo = updateUserName;
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
        inputErrors: [["firstName", "First name is required."]],
      });
    }

    const lastName = cleanDisplayStr(input.lastName);
    if (!lastName) {
      return D2Result.validationFailed({
        inputErrors: [["lastName", "Last name is required."]],
      });
    }

    const combinedName = `${firstName} ${lastName}`;

    // Update Geo contact via atomic replacement (contextKey + relatedEntityId lookup).
    // Cross-service call — do NOT bubbleFail (may leak Geo internals).
    const contactToCreate: ContactToCreateDTO = {
      createdAt: new Date(),
      contextKey: GEO_CONTEXT_KEYS.USER,
      relatedEntityId: input.userId,
      contactMethods: undefined,
      personalDetails: {
        firstName,
        lastName,
        professionalCredentials: [],
      },
      professionalDetails: undefined,
      location: undefined,
    };

    const geoResult = await this.updateContactsByExtKeys.handleAsync({
      contacts: [contactToCreate],
    });
    if (!geoResult.success) {
      this.context.logger.error("Failed to update Geo contact for user real name", {
        userId: input.userId,
        errorCode: geoResult.errorCode,
        statusCode: geoResult.statusCode,
      });
      return D2Result.serviceUnavailable({
        messages: ["Unable to update contact details. Please try again."],
      });
    }

    // Update BetterAuth user.name (same-service repo — bubbleFail is safe)
    const nameResult = await this.updateUserNameRepo.handleAsync({
      userId: input.userId,
      name: combinedName,
    });
    if (!nameResult.success) {
      return D2Result.bubbleFail(nameResult);
    }

    return D2Result.ok({ data: { name: combinedName } });
  }
}

export type {
  UpdateUserRealNameInput,
  UpdateUserRealNameOutput,
} from "../../../../interfaces/cqrs/handlers/c/update-user-real-name.js";
