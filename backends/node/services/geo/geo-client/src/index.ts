export { type GeoClientOptions, DEFAULT_GEO_CLIENT_OPTIONS } from "./geo-client-options.js";
export { GEO_CACHE_KEYS } from "./cache-keys.js";

// Validation schemas (single source of truth for contact field constraints)
export {
  contactMethodsSchema,
  personalDetailsSchema,
  professionalDetailsSchema,
  locationInputSchema,
  contactInputSchema,
} from "./validation/contact-schemas.js";
// gRPC helpers
export { createApiKeyInterceptor } from "./grpc/api-key-interceptor.js";
export { createGeoServiceClient } from "./grpc/create-geo-client.js";
export { createGeoCircuitBreaker } from "./circuit-breaker-factory.js";

// Interfaces (contract types + handler interfaces + redaction constants)
export { Commands, Queries, Complex } from "./interfaces/index.js";
export type { Subs } from "./interfaces/index.js";

// Commands
export { ReqUpdate } from "./handlers/c/req-update.js";
export type { ReqUpdateInput, ReqUpdateOutput } from "./interfaces/c/req-update.js";
export { SetInMem } from "./handlers/c/set-in-mem.js";
export type { SetInMemInput, SetInMemOutput } from "./interfaces/c/set-in-mem.js";
export { SetInDist, GeoRefDataSerializer } from "./handlers/c/set-in-dist.js";
export type { SetInDistInput, SetInDistOutput } from "./interfaces/c/set-in-dist.js";
export { SetOnDisk } from "./handlers/c/set-on-disk.js";
export type { SetOnDiskInput, SetOnDiskOutput } from "./interfaces/c/set-on-disk.js";
export { CreateContacts } from "./handlers/c/create-contacts.js";
export type { CreateContactsInput, CreateContactsOutput } from "./interfaces/c/create-contacts.js";
export { DeleteContactsByExtKeys } from "./handlers/c/delete-contacts-by-ext-keys.js";
export type {
  DeleteContactsByExtKeysInput,
  DeleteContactsByExtKeysOutput,
} from "./interfaces/c/delete-contacts-by-ext-keys.js";

// Queries
export { GetFromMem } from "./handlers/q/get-from-mem.js";
export type { GetFromMemInput, GetFromMemOutput } from "./interfaces/q/get-from-mem.js";
export { GetFromDist } from "./handlers/q/get-from-dist.js";
export type { GetFromDistInput, GetFromDistOutput } from "./interfaces/q/get-from-dist.js";
export { GetFromDisk } from "./handlers/q/get-from-disk.js";
export type { GetFromDiskInput, GetFromDiskOutput } from "./interfaces/q/get-from-disk.js";
export { GetContactsByExtKeys } from "./handlers/q/get-contacts-by-ext-keys.js";
export type {
  GetContactsByExtKeysInput,
  GetContactsByExtKeysOutput,
  IGetContactsByExtKeysHandler,
} from "./interfaces/q/get-contacts-by-ext-keys.js";
export { GetContactsByIds } from "./handlers/q/get-contacts-by-ids.js";
export type {
  GetContactsByIdsInput,
  GetContactsByIdsOutput,
} from "./interfaces/q/get-contacts-by-ids.js";

// Complex
export { FindWhoIs } from "./handlers/x/find-whois.js";
export type { FindWhoIsInput, FindWhoIsOutput } from "./interfaces/x/find-whois.js";
export { Get, type GetDeps } from "./handlers/x/get.js";
export type { GetInput, GetOutput } from "./interfaces/x/get.js";
export { UpdateContactsByExtKeys } from "./handlers/x/update-contacts-by-ext-keys.js";
export type {
  UpdateContactsByExtKeysInput,
  UpdateContactsByExtKeysOutput,
} from "./interfaces/x/update-contacts-by-ext-keys.js";

// Messaging
export { Updated, type UpdatedDeps } from "./messaging/handlers/sub/updated.js";
export type { UpdatedOutput } from "./interfaces/sub/updated.js";
export { createUpdatedConsumer } from "./messaging/consumers/updated-consumer.js";
export { ContactsEvicted } from "./messaging/handlers/sub/contacts-evicted.js";
export type {
  ContactsEvictedOutput,
  IContactsEvictedHandler,
} from "./messaging/handlers/sub/contacts-evicted.js";
export { createContactsEvictedConsumer } from "./messaging/consumers/contacts-evicted-consumer.js";
export { wireGeoClientConsumers } from "./messaging/wire-consumers.js";
export type { WireGeoClientConsumersOptions } from "./messaging/wire-consumers.js";

// Service Keys (DI registration tokens)
export {
  IGeoServiceClientKey,
  ICreateContactsKey,
  IDeleteContactsByExtKeysKey,
  IGetContactsByExtKeysKey,
  IGetContactsByIdsKey,
  IFindWhoIsKey,
  IUpdateContactsByExtKeysKey,
} from "./service-keys.js";
