import type { IHandler } from "@d2/handler";
import type { ChannelPreference } from "@d2/comms-domain";

export interface GetChannelPreferenceInput {
  readonly contactId: string;
}

export interface GetChannelPreferenceOutput {
  readonly pref?: ChannelPreference;
}

/** Handler for retrieving channel preferences. */
export interface IGetChannelPreferenceHandler extends IHandler<
  GetChannelPreferenceInput,
  GetChannelPreferenceOutput
> {}
