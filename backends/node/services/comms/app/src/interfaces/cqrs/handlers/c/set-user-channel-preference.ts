import type { IHandler } from "@d2/handler";
import type { ChannelPreference } from "@d2/comms-domain";

/**
 * User-centric variant of SetChannelPreference — caller passes the contact's
 * (contextKey, relatedEntityId) tuple instead of the contactId. Internally
 * resolves the contact via the Geo client (memory-cached, refreshed via
 * cross-process eviction events) and delegates to the contact-id handler.
 */
export interface SetUserChannelPreferenceInput {
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly emailEnabled?: boolean;
  readonly smsEnabled?: boolean;
}

export interface SetUserChannelPreferenceOutput {
  readonly pref: ChannelPreference;
}

export interface ISetUserChannelPreferenceHandler
  extends IHandler<SetUserChannelPreferenceInput, SetUserChannelPreferenceOutput> {}
