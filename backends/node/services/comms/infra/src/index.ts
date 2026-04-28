// @d2/comms-infra — Infrastructure implementations for the Comms service.
// Drizzle repositories, Resend email provider, RabbitMQ consumers.

// --- Repository Handler Factories ---
export {
  createMessageRepoHandlers,
  createDeliveryRequestRepoHandlers,
  createDeliveryAttemptRepoHandlers,
  createChannelPreferenceRepoHandlers,
} from "./repository/handlers/factories.js";

// --- Drizzle Schema ---
export {
  message,
  deliveryRequest,
  deliveryAttempt,
  channelPreference,
} from "./repository/schema/index.js";

export type {
  MessageRow,
  NewMessage,
  DeliveryRequestRow,
  NewDeliveryRequest,
  DeliveryAttemptRow,
  NewDeliveryAttempt,
  ChannelPreferenceRow,
  NewChannelPreference,
} from "./repository/schema/index.js";

// --- Migrations ---
export { runMigrations } from "./repository/migrate.js";

// --- Providers ---
export { ResendEmailProvider } from "./providers/email/resend/resend-email-provider.js";
export { TwilioSmsProvider } from "./providers/sms/twilio/twilio-sms-provider.js";
export { MockSmsProvider } from "./providers/sms/mock/mock-sms-provider.js";

// --- Messaging ---
export { createNotificationConsumer } from "./messaging/consumers/notification-consumer.js";
export type { NotificationConsumerDeps } from "./messaging/consumers/notification-consumer.js";
export { declareRetryTopology, getRetryTierQueue } from "./messaging/retry-topology.js";

// --- Purge Handlers (used by integration tests) ---
export { PurgeDeletedMessages } from "./repository/handlers/d/purge-deleted-messages.js";
export { PurgeDeliveryHistory } from "./repository/handlers/d/purge-delivery-history.js";

// --- Repository Ping ---
export { PingDb } from "./repository/handlers/r/ping-db.js";

// --- DI Registration ---
export { addCommsInfra } from "./registration.js";
