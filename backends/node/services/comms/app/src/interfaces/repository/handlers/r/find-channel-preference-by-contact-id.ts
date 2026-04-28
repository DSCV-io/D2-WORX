import type { IHandler } from "@d2/handler";
import type { ChannelPreference } from "@d2/comms-domain";

export interface FindChannelPreferenceByContactIdInput {
  readonly contactId: string;
}

export interface FindChannelPreferenceByContactIdOutput {
  readonly pref?: ChannelPreference;
}

export type IFindChannelPreferenceByContactIdHandler = IHandler<
  FindChannelPreferenceByContactIdInput,
  FindChannelPreferenceByContactIdOutput
>;
