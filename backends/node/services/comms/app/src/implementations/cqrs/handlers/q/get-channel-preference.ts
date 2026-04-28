import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { Queries } from "../../../../interfaces/cqrs/handlers/index.js";
import { D2Result } from "@d2/result";
import type { ChannelPreference } from "@d2/comms-domain";
import type { ChannelPreferenceRepoHandlers } from "../../../../interfaces/repository/handlers/index.js";
import type { InMemoryCache } from "@d2/interfaces";
import { COMMS_CACHE_KEYS } from "../../../../cache-keys.js";

type Input = Queries.GetChannelPreferenceInput;
type Output = Queries.GetChannelPreferenceOutput;

const getChannelPreferenceSchema = z.object({
  contactId: zodGuid,
});

export class GetChannelPreference
  extends BaseHandler<Input, Output>
  implements Queries.IGetChannelPreferenceHandler
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
    const validation = this.validateInput(getChannelPreferenceSchema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const cacheKey = COMMS_CACHE_KEYS.channelPref(input.contactId);

    // Check cache first
    if (this.cache) {
      const cached = await this.cache.get.handleAsync({ key: cacheKey });
      if (cached.success && cached.data?.value !== undefined) {
        return D2Result.ok({ data: { pref: cached.data.value } });
      }
    }

    // Fetch from DB
    let pref: ChannelPreference | undefined;
    const result = await this.repo.findByContactId.handleAsync({ contactId: input.contactId });
    if (result.success && result.data) pref = result.data.pref;

    // Populate cache on miss
    if (this.cache && pref) {
      await this.cache.set.handleAsync({ key: cacheKey, value: pref, expirationMs: 900_000 });
    }

    if (!pref) {
      return D2Result.notFound();
    }

    return D2Result.ok({ data: { pref } });
  }
}

export type {
  GetChannelPreferenceInput,
  GetChannelPreferenceOutput,
} from "../../../../interfaces/cqrs/handlers/q/get-channel-preference.js";
