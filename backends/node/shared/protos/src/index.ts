// Common types
export type { D2ResultProto, InputErrorProto } from "./generated/common/v1/d2_result.js";

export {
  D2ResultProto as D2ResultProtoFns,
  InputErrorProto as InputErrorProtoFns,
} from "./generated/common/v1/d2_result.js";

// Health check types (shared by all services)
export type {
  CheckHealthRequest,
  CheckHealthResponse,
  ComponentHealth,
} from "./generated/common/v1/health.js";

export {
  CheckHealthRequest as CheckHealthRequestFns,
  CheckHealthResponse as CheckHealthResponseFns,
  ComponentHealth as ComponentHealthFns,
} from "./generated/common/v1/health.js";

// Ping service
export type {
  PingRequest,
  PingResponse,
  PingServiceClient,
  PingServiceServer,
} from "./generated/common/v1/ping.js";

export {
  PingServiceClient as PingServiceClientCtor,
  PingServiceService,
} from "./generated/common/v1/ping.js";

// Geo service — DTOs
export type {
  CountryDTO,
  SubdivisionDTO,
  CurrencyDTO,
  LanguageDTO,
  LocaleDTO,
  TimezoneDTO,
  GeopoliticalEntityDTO,
  CoordinatesDTO,
  StreetAddressDTO,
  EmailAddressDTO,
  PhoneNumberDTO,
  PersonalDTO,
  ProfessionalDTO,
  ContactMethodsDTO,
  LocationDTO,
  WhoIsDTO,
  ContactDTO,
  ContactToCreateDTO,
} from "./generated/geo/v1/geo.js";

// GeoRefData — value export (provides encode/decode for proto serialization)
export { GeoRefData } from "./generated/geo/v1/geo.js";

// Geo service — request/response types
export type {
  GetReferenceDataRequest,
  GetReferenceDataResponse,
  RequestReferenceDataUpdateRequest,
  RequestReferenceDataUpdateResponse,
  FindWhoIsRequest,
  FindWhoIsResponse,
  FindWhoIsKeys,
  FindWhoIsData,
  GetContactsRequest,
  GetContactsResponse,
  GetContactsByExtKeysRequest,
  GetContactsByExtKeysResponse,
  GetContactsExtKeys,
  GetContactsByExtKeysData,
  CreateContactsRequest,
  CreateContactsResponse,
  DeleteContactsRequest,
  DeleteContactsResponse,
  DeleteContactsByExtKeysRequest,
  DeleteContactsByExtKeysResponse,
  UpdateContactsByExtKeysRequest,
  UpdateContactsByExtKeysResponse,
  ContactReplacementKey,
  ContactReplacement,
  GeoServiceClient,
  GeoServiceServer,
} from "./generated/geo/v1/geo.js";

export {
  GeoServiceClient as GeoServiceClientCtor,
  GeoServiceService,
} from "./generated/geo/v1/geo.js";

// Timestamp (well-known type)
export type { Timestamp } from "./generated/google/protobuf/timestamp.js";

// Events — Geo
export type {
  GeoRefDataUpdatedEvent,
  EvictedContact,
  ContactsEvictedEvent,
} from "./generated/events/v1/geo_events.js";

export {
  GeoRefDataUpdatedEvent as GeoRefDataUpdatedEventFns,
  EvictedContact as EvictedContactFns,
  ContactsEvictedEvent as ContactsEvictedEventFns,
} from "./generated/events/v1/geo_events.js";

// Comms service — DTOs
export type {
  PaginationRequest as CommsPaginationRequest,
  ChannelPreferenceDTO,
  DeliveryRequestDTO,
  DeliveryAttemptDTO,
  ThreadDTO,
  MessageDTO as CommsMessageDTO,
  NotificationDTO,
  ReactionDTO,
  ParticipantDTO,
} from "./generated/comms/v1/comms.js";

// Comms service — DTO value exports (encode/decode for roundtrip tests)
export {
  ChannelPreferenceDTO as ChannelPreferenceDTOFns,
  DeliveryRequestDTO as DeliveryRequestDTOFns,
  DeliveryAttemptDTO as DeliveryAttemptDTOFns,
} from "./generated/comms/v1/comms.js";

// Comms service — request/response types + client/server
export type {
  GetChannelPreferenceRequest,
  GetChannelPreferenceResponse,
  SetChannelPreferenceRequest,
  SetChannelPreferenceResponse,
  GetDeliveryStatusRequest,
  GetDeliveryStatusResponse,
  GetNotificationsRequest,
  GetNotificationsResponse,
  MarkNotificationsReadRequest,
  MarkNotificationsReadResponse,
  CreateThreadRequest,
  CreateThreadResponse,
  GetThreadRequest,
  GetThreadResponse,
  GetThreadsRequest,
  GetThreadsResponse,
  PostMessageRequest,
  PostMessageResponse,
  EditMessageRequest,
  EditMessageResponse,
  DeleteMessageRequest,
  DeleteMessageResponse,
  GetThreadMessagesRequest,
  GetThreadMessagesResponse,
  AddReactionRequest,
  AddReactionResponse,
  RemoveReactionRequest,
  RemoveReactionResponse,
  AddParticipantRequest,
  AddParticipantResponse,
  RemoveParticipantRequest,
  RemoveParticipantResponse,
  CommsServiceClient,
  CommsServiceServer,
} from "./generated/comms/v1/comms.js";

// Comms service — value exports (client constructor + service definition)
export {
  CommsServiceClient as CommsServiceClientCtor,
  CommsServiceService,
} from "./generated/comms/v1/comms.js";

// Job types (shared)
export type {
  TriggerJobRequest,
  TriggerJobResponse,
  JobExecutionData,
} from "./generated/common/v1/jobs.js";

export {
  TriggerJobRequest as TriggerJobRequestFns,
  TriggerJobResponse as TriggerJobResponseFns,
  JobExecutionData as JobExecutionDataFns,
} from "./generated/common/v1/jobs.js";

// Auth service — client/server
export type { AuthServiceClient, AuthServiceServer } from "./generated/auth/v1/auth.js";

export {
  AuthServiceClient as AuthServiceClientCtor,
  AuthServiceService,
} from "./generated/auth/v1/auth.js";

// Auth job service
export type { AuthJobServiceClient, AuthJobServiceServer } from "./generated/auth/v1/auth_jobs.js";

export {
  AuthJobServiceClient as AuthJobServiceClientCtor,
  AuthJobServiceService,
} from "./generated/auth/v1/auth_jobs.js";

// Geo job service
export type { GeoJobServiceClient, GeoJobServiceServer } from "./generated/geo/v1/geo_jobs.js";

export {
  GeoJobServiceClient as GeoJobServiceClientCtor,
  GeoJobServiceService,
} from "./generated/geo/v1/geo_jobs.js";

// Comms job service
export type {
  CommsJobServiceClient,
  CommsJobServiceServer,
} from "./generated/comms/v1/comms_jobs.js";

export {
  CommsJobServiceClient as CommsJobServiceClientCtor,
  CommsJobServiceService,
} from "./generated/comms/v1/comms_jobs.js";

// Files service — FileCallback (implemented by owning services, called by Files)
export type {
  CanAccessRequest,
  CanAccessResponse,
  FileProcessedRequest,
  FileProcessedResponse,
  FileCallbackClient,
  FileCallbackServer,
} from "./generated/files/v1/files.js";

export {
  FileCallbackClient as FileCallbackClientCtor,
  FileCallbackService,
} from "./generated/files/v1/files.js";

// Files service — message Fns (encode/decode for roundtrip)
export {
  CanAccessRequest as CanAccessRequestFns,
  CanAccessResponse as CanAccessResponseFns,
  FileProcessedRequest as FileProcessedRequestFns,
  FileProcessedResponse as FileProcessedResponseFns,
} from "./generated/files/v1/files.js";

// Files service — FilesService (inbound gRPC API)
export type { FilesServiceClient, FilesServiceServer } from "./generated/files/v1/files_service.js";

export {
  FilesServiceClient as FilesServiceClientCtor,
  FilesServiceService,
} from "./generated/files/v1/files_service.js";

// Files job service — FilesJobService (inbound gRPC API)
export type {
  FilesJobServiceClient,
  FilesJobServiceServer,
} from "./generated/files/v1/files_jobs.js";

export {
  FilesJobServiceClient as FilesJobServiceClientCtor,
  FilesJobServiceService,
} from "./generated/files/v1/files_jobs.js";

// Realtime Gateway — general-purpose push gateway (channel-based routing)
export type {
  PushToChannelRequest,
  RemoveFromChannelRequest,
  PushResponse as RealtimePushResponse,
  RealtimeGatewayClient,
  RealtimeGatewayServer,
} from "./generated/realtime/v1/realtime_gateway.js";

export {
  RealtimeGatewayClient as RealtimeGatewayClientCtor,
  RealtimeGatewayService,
} from "./generated/realtime/v1/realtime_gateway.js";

export {
  PushToChannelRequest as PushToChannelRequestFns,
  RemoveFromChannelRequest as RemoveFromChannelRequestFns,
  PushResponse as RealtimePushResponseFns,
} from "./generated/realtime/v1/realtime_gateway.js";
