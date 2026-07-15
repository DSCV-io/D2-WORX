// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

export type { TelemetryOptions } from "./telemetry-options.js";
export { type TelemetryHandle, setupTelemetry } from "./setup-telemetry.js";
export { buildPropagators } from "./propagators.js";
export {
  MessagingActivityTags,
  type MessagingActivityTag,
  ALL_MESSAGING_ACTIVITY_TAGS,
} from "./otel-messaging-tags.g.js";
