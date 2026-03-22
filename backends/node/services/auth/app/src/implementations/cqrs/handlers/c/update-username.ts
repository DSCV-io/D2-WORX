import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  ICheckUsernameAvailableHandler,
  IUpdateUserUsernameHandler,
} from "../../../../interfaces/repository/handlers/index.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Commands.UpdateUsernameInput;
type Output = Commands.UpdateUsernameOutput;

const USERNAME_MIN_LENGTH = 3;
const USERNAME_MAX_LENGTH = 32;

/** Usernames: letters (a-z, A-Z) and digits (0-9) only. */
const USERNAME_REGEX = /^[a-zA-Z0-9]+$/;

const schema = z.object({
  userId: zodGuid,
  username: z.string().min(USERNAME_MIN_LENGTH).max(USERNAME_MAX_LENGTH),
});

/**
 * Updates a user's username.
 *
 * Validates the username (alphanumeric only, length), checks uniqueness
 * via case-insensitive DB query, and updates both username (lowercased) and
 * displayUsername (original casing) in the user table.
 *
 * IDOR prevention: userId is injected from IRequestContext, never from input.
 */
export class UpdateUsername
  extends BaseHandler<Input, Output>
  implements Commands.IUpdateUsernameHandler
{
  private readonly checkAvailable: ICheckUsernameAvailableHandler;
  private readonly updateUsernameRepo: IUpdateUserUsernameHandler;

  constructor(
    checkAvailable: ICheckUsernameAvailableHandler,
    updateUsername: IUpdateUserUsernameHandler,
    context: IHandlerContext,
  ) {
    super(context);
    this.checkAvailable = checkAvailable;
    this.updateUsernameRepo = updateUsername;
  }

  override get redaction() {
    return Commands.UPDATE_USERNAME_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const trimmed = input.username.trim();
    if (!trimmed) {
      return D2Result.validationFailed({
        inputErrors: [["username", "Username is required."]],
      });
    }

    if (!USERNAME_REGEX.test(trimmed)) {
      return D2Result.validationFailed({
        inputErrors: [["username", "Username can only contain letters and numbers."]],
      });
    }

    if (trimmed.length < USERNAME_MIN_LENGTH) {
      return D2Result.validationFailed({
        inputErrors: [["username", `Username must be at least ${USERNAME_MIN_LENGTH} characters.`]],
      });
    }

    const displayUsername = trimmed;
    const username = trimmed.toLowerCase();

    // Check uniqueness (case-insensitive — column stores lowercased values)
    const availResult = await this.checkAvailable.handleAsync({ username });
    if (!availResult.success || !availResult.data) {
      return D2Result.bubbleFail(availResult);
    }

    if (!availResult.data.available) {
      return D2Result.validationFailed({
        inputErrors: [["username", "Username is already taken."]],
      });
    }

    // Update both username and displayUsername
    const updateResult = await this.updateUsernameRepo.handleAsync({
      userId: input.userId,
      username,
      displayUsername,
    });
    if (!updateResult.success) {
      return D2Result.bubbleFail(updateResult);
    }

    return D2Result.ok({ data: { username, displayUsername } });
  }
}

export type {
  UpdateUsernameInput,
  UpdateUsernameOutput,
} from "../../../../interfaces/cqrs/handlers/c/update-username.js";
