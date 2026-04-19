import type { ServiceCollection } from "@d2/di";
import type { HandlerContext } from "@d2/handler";
import * as CacheMemory from "@d2/cache-memory";
import {
  ContactsEvicted,
  GetContactsByExtKeys,
  GetContactsByIds,
  IGetContactsByExtKeysKey,
  IGetContactsByIdsKey,
  createGeoServiceClient,
  DEFAULT_GEO_CLIENT_OPTIONS,
} from "@d2/geo-client";

/**
 * Result of {@link addGeoClientHandlers} — exposes the contact cache store and
 * eviction handler so the composition-root can wire the cross-process eviction
 * consumer (subscribes to Geo's `ContactsEvictedEvent` over RabbitMQ).
 */
export interface GeoClientSetup {
  readonly contactCacheStore: CacheMemory.MemoryCacheStore;
  readonly contactsEvictedHandler: ContactsEvicted;
}

/**
 * Creates the Geo service client + contact handlers and registers them as
 * singletons in the DI container. Returns the cache store and eviction handler
 * so the caller can wire the cross-process invalidation consumer.
 */
export function addGeoClientHandlers(
  services: ServiceCollection,
  config: { geoAddress?: string; geoApiKey?: string },
  serviceContext: HandlerContext,
): GeoClientSetup {
  if (!config.geoAddress || !config.geoApiKey) {
    throw new Error(
      "GEO_GRPC_ADDRESS and GEO_API_KEY are required — comms service cannot start without Geo",
    );
  }

  const contactCacheStore = new CacheMemory.MemoryCacheStore();
  const geoClient = createGeoServiceClient(config.geoAddress, config.geoApiKey);
  const geoOptions = { ...DEFAULT_GEO_CLIENT_OPTIONS, apiKey: config.geoApiKey };

  const getContactsByIds = new GetContactsByIds(
    contactCacheStore,
    geoClient,
    geoOptions,
    serviceContext,
  );
  services.addInstance(IGetContactsByIdsKey, getContactsByIds);

  const getContactsByExtKeys = new GetContactsByExtKeys(
    contactCacheStore,
    geoClient,
    geoOptions,
    serviceContext,
  );
  services.addInstance(IGetContactsByExtKeysKey, getContactsByExtKeys);

  const contactsEvictedHandler = new ContactsEvicted(contactCacheStore, serviceContext);

  return { contactCacheStore, contactsEvictedHandler };
}
