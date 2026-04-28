import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import { D2Result } from "@d2/result";
import { createChannelPreference, updateChannelPreference } from "@d2/comms-domain";
import type { ChannelPreference } from "@d2/comms-domain";
import type { ChannelPreferenceRepoHandlers } from "../../../../interfaces/repository/handlers/index.js";
import type { InMemoryCache } from "@d2/interfaces";
import { COMMS_CACHE_KEYS } from "../../../../cache-keys.js";

type Input = Commands.SetChannelPreferenceInput;
type Output = Commands.SetChannelPreferenceOutput;

const schema = z.object({
  contactId: zodGuid,
  emailEnabled: z.boolean().optional(),
  smsEnabled: z.boolean().optional(),
});

export class SetChannelPreference
  extends BaseHandler<Input, Output>
  implements Commands.ISetChannelPreferenceHandler
{
  private readonly repo: ChannelPreferenceRepoHandlers;
  private readonly cache?: {
    get: InMemoryCache.IGetHandler<ChannelPreference>;
    set: InMemoryCache.ISetHandler<ChannelPreference>;
  };

  constructor(
    repo: ChannelPreferenceRepoHandlers,
    context: IHandlerContext,
    cache?: {
      get: InMemoryCache.IGetHandler<ChannelPreference>;
      set: InMemoryCache.ISetHandler<ChannelPreference>;
    },
  ) {
    super(context);
    this.repo = repo;
    this.cache = cache;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    // Check if existing pref exists
    let existing: ChannelPreference | undefined;
    const result = await this.repo.findByContactId.handleAsync({ contactId: input.contactId });
    if (result.success && result.data) existing = result.data.pref;

    let pref: ChannelPreference;
    if (existing) {
      pref = updateChannelPreference(existing, {
        emailEnabled: input.emailEnabled,
        smsEnabled: input.smsEnabled,
      });
      const updateResult = await this.repo.update.handleAsync({ pref });
      if (!updateResult.success) return D2Result.bubbleFail(updateResult);
    } else {
      pref = createChannelPreference({
        contactId: input.contactId,
        emailEnabled: input.emailEnabled,
        smsEnabled: input.smsEnabled,
      });
      const createResult = await this.repo.create.handleAsync({ pref });
      if (!createResult.success) return D2Result.bubbleFail(createResult);
    }

    // Evict + repopulate cache
    if (this.cache) {
      const cacheKey = COMMS_CACHE_KEYS.channelPref(input.contactId);
      await this.cache.set.handleAsync({ key: cacheKey, value: pref, expirationMs: 900_000 });
    }

    return D2Result.ok({ data: { pref } });
  }
}

export type {
  SetChannelPreferenceInput,
  SetChannelPreferenceOutput,
} from "../../../../interfaces/cqrs/handlers/c/set-channel-preference.js";
