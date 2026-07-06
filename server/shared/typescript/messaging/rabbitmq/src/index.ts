// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Public surface — CONSUMER-ONLY. There is deliberately NO publisher API: a TS
// publisher must not ship without .NET's structural publish/encrypt fusion (see
// the package README). The DLQ republish path is an internal detail of the
// consume pipeline, not a general publish API.

// Connection.
export {
  type RabbitMqConnectionOptions,
  createConnection,
  redactAmqpUri,
} from "./connection/connection-options.js";
export type { Connection } from "rabbitmq-client";

// Subscription.
export {
  type SubscribeOptions,
  type Subscription,
  subscribe,
} from "./subscribing/subscriber.js";
export {
  type MessageHandler,
  type RepublishFn,
} from "./subscribing/delivery-processor.js";
export { type ConsumedMessage } from "./subscribing/consumed-message.js";

// Topology contract.
export {
  type SubscriptionDescriptor,
  type TieredRetryDescriptor,
  resolveQueueName,
} from "./topology/subscription-descriptor.js";
export { QueuePattern } from "./topology/queue-pattern.js";
export { DlqNaming } from "./topology/dlq-naming.js";

// Per-message context establishment.
export {
  type ConsumeContext,
  type MutablePropagatedContext,
  applyPropagatedContext,
  establishConsumeContext,
} from "./context/consume-context.js";

// Body-decompose seam.
export {
  type BodyOpener,
  PlaintextBodyOpener,
} from "./subscribing/body-opener.js";
export { MessageBodyDecodeError } from "./subscribing/message-body-decode-error.js";

// Idempotency.
export {
  type IMessageIdempotencyStore,
  InMemoryMessageIdempotencyStore,
} from "./idempotency/message-idempotency-store.js";

// Telemetry.
export { MESSAGING_SOURCE_NAME } from "./telemetry.js";
