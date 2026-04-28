import { randomUUID } from "node:crypto";
import type { ILogger } from "@d2/logging";
import type { MessageBus, IMessageConsumer } from "@d2/messaging";
import type { MemoryCacheStore } from "@d2/cache-memory";
import type { IHandlerContext } from "@d2/handler";
import { ContactsEvicted } from "./handlers/sub/contacts-evicted.js";
import type { Updated } from "./handlers/sub/updated.js";
import { createContactsEvictedConsumer } from "./consumers/contacts-evicted-consumer.js";
import { createUpdatedConsumer } from "./consumers/updated-consumer.js";

/**
 * Geo publishes domain events on per-area fanout exchanges. Mirrors the .NET
 * `AmqpConventions.EventExchange("...")` naming.
 */
const GEO_REF_DATA_EXCHANGE = "events.geo";
const GEO_CONTACTS_EXCHANGE = "events.geo.contacts";

export interface WireGeoClientConsumersOptions {
  /** Connected MessageBus from the consuming service. */
  readonly bus: MessageBus;
  /**
   * Geo-client's local cache store — same instance used by the in-process
   * cache-aside handlers. Used directly to construct the `ContactsEvicted`
   * handler (which only needs the store).
   */
  readonly cacheStore: MemoryCacheStore;
  /** Handler context for the constructed `ContactsEvicted` subscription. */
  readonly context: IHandlerContext;
  /** Service logger — used for consumer-level lifecycle logs. */
  readonly logger: ILogger;
  /**
   * Optional `Updated` handler factory. When provided, also wires the geo
   * ref-data update consumer. Omit if your service doesn't cache ref data.
   * `Updated` needs multiple geo-client handlers as deps so the caller must
   * supply it from their DI scope.
   */
  readonly updatedHandlerFactory?: () => Updated;
  /**
   * Per-instance suffix appended to the queue name. Default: random 8-char hex.
   * Override only for deterministic testing.
   */
  readonly instanceId?: string;
}

export interface WireGeoClientConsumersResult {
  readonly contactsEvictedConsumer: IMessageConsumer;
  readonly refDataConsumer?: IMessageConsumer;
}

/**
 * Wires the geo-client's cross-process cache invalidation consumers — once,
 * inside a single function that consuming services call from their composition
 * root after the MessageBus is connected. Mirrors the .NET pattern where
 * `AddGeoRefDataConsumer` registers both `BackgroundService`s automatically.
 *
 * Wires:
 *   - `events.geo.contacts` (fanout) → `ContactsEvicted` → evict contact caches
 *     when contacts are mutated by another process. Always wired.
 *   - `events.geo` (fanout) → `Updated` → evict ref-data caches on Geo update.
 *     Wired only when `updatedHandlerFactory` is supplied.
 *
 * Each consumer gets its own auto-deleted queue (`{exchange}.{instanceId}`)
 * bound to the fanout exchange, so every process receives every event.
 */
export async function wireGeoClientConsumers(
  options: WireGeoClientConsumersOptions,
): Promise<WireGeoClientConsumersResult> {
  const instanceId = options.instanceId ?? randomUUID().replace(/-/g, "").slice(0, 8);

  const contactsEvictedHandler = new ContactsEvicted(options.cacheStore, options.context);
  const contactsEvictedConsumer = createContactsEvictedConsumer(
    options.bus,
    {
      queue: `${GEO_CONTACTS_EXCHANGE}.${instanceId}`,
      queueOptions: { durable: false, arguments: { "x-expires": 60_000 } },
      exchanges: [{ exchange: GEO_CONTACTS_EXCHANGE, type: "fanout", durable: true }],
      queueBindings: [{ exchange: GEO_CONTACTS_EXCHANGE, routingKey: "" }],
    },
    () => contactsEvictedHandler,
    options.logger,
  );
  await contactsEvictedConsumer.ready;

  let refDataConsumer: IMessageConsumer | undefined;
  if (options.updatedHandlerFactory) {
    refDataConsumer = createUpdatedConsumer(
      options.bus,
      {
        queue: `${GEO_REF_DATA_EXCHANGE}.${instanceId}`,
        queueOptions: { durable: false, arguments: { "x-expires": 60_000 } },
        exchanges: [{ exchange: GEO_REF_DATA_EXCHANGE, type: "fanout", durable: true }],
        queueBindings: [{ exchange: GEO_REF_DATA_EXCHANGE, routingKey: "" }],
      },
      options.updatedHandlerFactory,
      options.logger,
    );
    await refDataConsumer.ready;
  }

  options.logger.info("Geo client cache-invalidation consumers ready", {
    contactsExchange: GEO_CONTACTS_EXCHANGE,
    refDataExchangeWired: !!refDataConsumer,
    instanceId,
  });

  return { contactsEvictedConsumer, refDataConsumer };
}
