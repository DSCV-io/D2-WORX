// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Public surface — consumer AND publisher. The publisher ships with .NET's
// structural publish/encrypt fusion mirrored at the type level: `createPublisher`
// binds a compile-time type witness (an unwired encrypted domain is a COMPILE
// error, no raw-bytes publish surface) plus a runtime default-deny second lock.

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
export {
  CryptoBodyOpener,
  assertOpenerMatchesDomain,
} from "./subscribing/crypto-body-opener.js";
export { MessageBodyDecodeError } from "./subscribing/message-body-decode-error.js";

// Publisher — the auto-encrypting publish/encrypt fusion.
export {
  createPublisher,
  publishVia,
  type D2Publisher,
  type CreatePublisherOptions,
  type PublishEnvelope,
  type PublishSender,
} from "./publishing/publisher.js";
export {
  composeBody,
  type Composer,
  type ComposedBody,
} from "./publishing/body-composer.js";
export {
  type DomainCryptoMap,
  type ComposerFor,
  type EncryptedDomain,
  type PublishableKey,
  type PublishableKeyOf,
  type CatalogEncryption,
} from "./publishing/domain-crypto-map.js";
export { readEncryptionKid } from "./publishing/encryption-kid.js";

// Idempotency.
export {
  type IMessageIdempotencyStore,
  InMemoryMessageIdempotencyStore,
} from "./idempotency/message-idempotency-store.js";

// Telemetry.
export { MESSAGING_SOURCE_NAME } from "./telemetry.js";
