import type { IHandler } from "@d2/handler";
import type { ChannelPreference } from "@d2/comms-domain";

/**
 * User-centric variant of GetChannelPreference — caller passes the contact's
 * (contextKey, relatedEntityId) tuple instead of the contactId. Internally
 * resolves the contact via the Geo client (memory-cached, refreshed via
 * cross-process eviction events) and delegates to the contact-id handler.
 */
export interface GetUserChannelPreferenceInput {
  readonly contextKey: string;
  readonly relatedEntityId: string;
}

export interface GetUserChannelPreferenceOutput {
  readonly pref?: ChannelPreference;
}

export interface IGetUserChannelPreferenceHandler extends IHandler<
  GetUserChannelPreferenceInput,
  GetUserChannelPreferenceOutput
> {}
