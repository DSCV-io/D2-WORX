// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Browser-side REST client for the current user's notification preferences.
 *
 * Calls the .NET REST gateway directly (no SvelteKit proxy). The gateway
 * extracts the user id from the JWT and forwards to Comms via gRPC; Comms
 * resolves the user's auth_user contact internally (memory-cached, refreshed
 * via cross-process eviction events).
 */
import { type D2Result } from "@d2/result";
import { apiCall } from "./gateway-client.js";

const PATH = "/api/v1/notification-preferences";

export interface ChannelPreferenceDto {
  contactId: string;
  emailEnabled: boolean;
  smsEnabled: boolean;
  createdAt?: string;
  updatedAt?: string;
}

/** GET — returns the user's saved preferences, or no body when none are set yet. */
export async function getMyNotificationPreferences(): Promise<
  D2Result<ChannelPreferenceDto | undefined>
> {
  return apiCall<ChannelPreferenceDto | undefined>(PATH, { method: "GET" });
}

/** PUT — partial update; only provided fields are written. */
export async function setMyNotificationPreferences(input: {
  emailEnabled?: boolean;
  smsEnabled?: boolean;
}): Promise<D2Result<ChannelPreferenceDto | undefined>> {
  return apiCall<ChannelPreferenceDto | undefined>(PATH, {
    method: "PUT",
    body: input,
  });
}
