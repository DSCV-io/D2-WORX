import { z } from "zod";
import { BaseHandler, type IHandlerContext, type RedactionSpec, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { IUpdateUserTimezoneHandler as IUpdateUserTimezoneRepoHandler } from "../../../../interfaces/repository/handlers/index.js";
import type { IPushUserUpdated } from "../../../../interfaces/realtime/handlers/index.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { IInvalidateUserSessionCacheHandler } from "../../../../interfaces/cqrs/handlers/c/invalidate-user-session-cache.js";

type Input = Commands.UpdateUserTimezoneInput;
type Output = Commands.UpdateUserTimezoneOutput;

const schema = z.object({
  userId: zodGuid,
  timezone: z.string().min(1).max(64),
});

/** Redaction spec — timezone is not PII. */
export const UPDATE_USER_TIMEZONE_REDACTION: RedactionSpec = {};

/**
 * Updates a user's timezone preference.
 *
 * Unlike locale, timezone has no corresponding Geo contact field,
 * so this handler only:
 * 1. Validates input
 * 2. Updates user.timezone via repo handler
 * 3. Invalidates session cache + pushes SignalR event
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

  private readonly updateUserTimezoneRepo: IUpdateUserTimezoneRepoHandler;
  private readonly pushUserUpdated?: IPushUserUpdated;
  private readonly invalidateSessionCache?: IInvalidateUserSessionCacheHandler;

  constructor(
    updateUserTimezoneRepo: IUpdateUserTimezoneRepoHandler,
    context: IHandlerContext,
    pushUserUpdated?: IPushUserUpdated,
    invalidateSessionCache?: IInvalidateUserSessionCacheHandler,
  ) {
    super(context);
    this.updateUserTimezoneRepo = updateUserTimezoneRepo;
    this.pushUserUpdated = pushUserUpdated;
    this.invalidateSessionCache = invalidateSessionCache;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    // Update BetterAuth user.timezone (same-service repo — bubbleFail is safe)
    const timezoneResult = await this.updateUserTimezoneRepo.handleAsync({
      userId: input.userId,
      timezone: input.timezone,
    });
    if (!timezoneResult.success) {
      return D2Result.bubbleFail(timezoneResult);
    }

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
