import type { Channel } from "../enums/channel.js";
import type { Message } from "../entities/message.js";
import type { ChannelPreference } from "../entities/channel-preference.js";
import type { ResolvedChannels } from "../value-objects/resolved-channels.js";
import { CHANNELS } from "../enums/channel.js";

/**
 * Resolves which channels should receive delivery attempts for a message,
 * given the recipient's preferences and the message's channel/urgency settings.
 *
 * Resolution rules:
 * 1. `urgency="urgent"` -> all channels (email + SMS), ignores prefs + channels
 * 2. `channels` non-empty -> exactly those channels (caller override, ignores prefs)
 * 3. `channels` empty/undefined -> respects recipient channel preferences
 *
 * @param prefs - Recipient's channel preferences (undefined = defaults: email+sms enabled)
 * @param message - The message being delivered (reads channels + urgency)
 */
export function resolveChannels(
  prefs: ChannelPreference | undefined,
  message: Pick<Message, "channels" | "urgency">,
): ResolvedChannels {
  const channels: Channel[] = [];
  const skippedChannels: Channel[] = [];

  // --- Urgent: force all channels ---
  if (message.urgency === "urgent") {
    channels.push(...CHANNELS);

    return { channels, skippedChannels };
  }

  // --- Explicit channels: use exactly what the caller specified ---
  if (message.channels.length > 0) {
    channels.push(...message.channels);

    for (const ch of CHANNELS) {
      if (!message.channels.includes(ch)) {
        skippedChannels.push(ch);
      }
    }

    return { channels, skippedChannels };
  }

  // --- No explicit channels: respect all preferences ---
  const emailEnabled = prefs?.emailEnabled ?? true;
  const smsEnabled = prefs?.smsEnabled ?? true;

  if (emailEnabled) channels.push("email");
  else skippedChannels.push("email");

  if (smsEnabled) channels.push("sms");
  else skippedChannels.push("sms");

  return { channels, skippedChannels };
}
