import type { ServiceCollection } from "@d2/di";
import type { HandlerContext } from "@d2/handler";
import * as CacheMemory from "@d2/cache-memory";
import {
  CreateContacts,
  DeleteContactsByExtKeys,
  GetContactsByExtKeys,
  UpdateContactsByExtKeys,
  ICreateContactsKey,
  IDeleteContactsByExtKeysKey,
  IGetContactsByExtKeysKey,
  IUpdateContactsByExtKeysKey,
  DEFAULT_GEO_CLIENT_OPTIONS,
  createGeoServiceClient,
  type GeoClientOptions,
} from "@d2/geo-client";
import { GEO_CONTEXT_KEYS } from "@d2/auth-domain";

export interface GeoClientSetup {
  /** Typed Geo gRPC client. */
  geoClient: ReturnType<typeof createGeoServiceClient>;
  /** Geo client options (including allowed context keys). */
  geoOptions: GeoClientOptions;
  /** GetContactsByExtKeys handler (used directly by auth callbacks). */
  getContactsByExtKeys: GetContactsByExtKeys;
  /** Local memory cache backing all per-process geo-client cache-asides — passed to wireGeoClientConsumers for cross-process invalidation. */
  contactCacheStore: CacheMemory.MemoryCacheStore;
}

/**
 * Creates the Geo service client and registers all geo-client CQRS handlers
 * as singleton instances in the DI container.
 */
export function addGeoClientHandlers(
  services: ServiceCollection,
  config: { geoAddress?: string; geoApiKey?: string },
  serviceContext: HandlerContext,
): GeoClientSetup {
  if (!config.geoAddress || !config.geoApiKey) {
    throw new Error(
      "GEO_GRPC_ADDRESS and GEO_API_KEY are required — auth service cannot start without Geo",
    );
  }

  const geoOptions: GeoClientOptions = {
    ...DEFAULT_GEO_CLIENT_OPTIONS,
    allowedContextKeys: [
      GEO_CONTEXT_KEYS.ORG_CONTACT,
      GEO_CONTEXT_KEYS.USER,
      GEO_CONTEXT_KEYS.ORG_INVITATION,
    ],
    apiKey: config.geoApiKey,
  };

  const contactCacheStore = new CacheMemory.MemoryCacheStore();
  const geoClient = createGeoServiceClient(config.geoAddress, config.geoApiKey);

  const createContacts = new CreateContacts(geoClient, geoOptions, serviceContext);
  const deleteContactsByExtKeys = new DeleteContactsByExtKeys(
    contactCacheStore,
    geoClient,
    geoOptions,
    serviceContext,
  );
  const getContactsByExtKeys = new GetContactsByExtKeys(
    contactCacheStore,
    geoClient,
    geoOptions,
    serviceContext,
  );
  const updateContactsByExtKeys = new UpdateContactsByExtKeys(
    contactCacheStore,
    geoClient,
    geoOptions,
    serviceContext,
  );

  services.addInstance(ICreateContactsKey, createContacts);
  services.addInstance(IDeleteContactsByExtKeysKey, deleteContactsByExtKeys);
  services.addInstance(IGetContactsByExtKeysKey, getContactsByExtKeys);
  services.addInstance(IUpdateContactsByExtKeysKey, updateContactsByExtKeys);

  return { geoClient, geoOptions, getContactsByExtKeys, contactCacheStore };
}
