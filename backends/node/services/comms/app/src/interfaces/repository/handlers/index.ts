// --- Handler type imports (used by bundle interfaces below) ---
import type { ICreateMessageRecordHandler } from "./c/create-message-record.js";
import type { ICreateDeliveryRequestRecordHandler } from "./c/create-delivery-request-record.js";
import type { ICreateDeliveryAttemptRecordHandler } from "./c/create-delivery-attempt-record.js";
import type { ICreateChannelPreferenceRecordHandler } from "./c/create-channel-preference-record.js";
import type { IFindMessageByIdHandler } from "./r/find-message-by-id.js";
import type { IFindDeliveryRequestByIdHandler } from "./r/find-delivery-request-by-id.js";
import type { IFindDeliveryRequestByCorrelationIdHandler } from "./r/find-delivery-request-by-correlation-id.js";
import type { IFindDeliveryAttemptsByRequestIdHandler } from "./r/find-delivery-attempts-by-request-id.js";
import type { IFindChannelPreferenceByContactIdHandler } from "./r/find-channel-preference-by-contact-id.js";
import type { IMarkDeliveryRequestProcessedHandler } from "./u/mark-delivery-request-processed.js";
import type { IUpdateDeliveryAttemptStatusHandler } from "./u/update-delivery-attempt-status.js";
import type { IUpdateChannelPreferenceRecordHandler } from "./u/update-channel-preference-record.js";

// --- Create (C) ---
export type {
  CreateMessageRecordInput,
  CreateMessageRecordOutput,
  ICreateMessageRecordHandler,
} from "./c/create-message-record.js";

export type {
  CreateDeliveryRequestRecordInput,
  CreateDeliveryRequestRecordOutput,
  ICreateDeliveryRequestRecordHandler,
} from "./c/create-delivery-request-record.js";

export type {
  CreateDeliveryAttemptRecordInput,
  CreateDeliveryAttemptRecordOutput,
  ICreateDeliveryAttemptRecordHandler,
} from "./c/create-delivery-attempt-record.js";

export type {
  CreateChannelPreferenceRecordInput,
  CreateChannelPreferenceRecordOutput,
  ICreateChannelPreferenceRecordHandler,
} from "./c/create-channel-preference-record.js";

// --- Read (R) ---
export type {
  FindMessageByIdInput,
  FindMessageByIdOutput,
  IFindMessageByIdHandler,
} from "./r/find-message-by-id.js";

export type {
  FindDeliveryRequestByIdInput,
  FindDeliveryRequestByIdOutput,
  IFindDeliveryRequestByIdHandler,
} from "./r/find-delivery-request-by-id.js";

export type {
  FindDeliveryRequestByCorrelationIdInput,
  FindDeliveryRequestByCorrelationIdOutput,
  IFindDeliveryRequestByCorrelationIdHandler,
} from "./r/find-delivery-request-by-correlation-id.js";

export type {
  FindDeliveryAttemptsByRequestIdInput,
  FindDeliveryAttemptsByRequestIdOutput,
  IFindDeliveryAttemptsByRequestIdHandler,
} from "./r/find-delivery-attempts-by-request-id.js";

export type {
  FindChannelPreferenceByContactIdInput,
  FindChannelPreferenceByContactIdOutput,
  IFindChannelPreferenceByContactIdHandler,
} from "./r/find-channel-preference-by-contact-id.js";

// --- Update (U) ---
export type {
  MarkDeliveryRequestProcessedInput,
  MarkDeliveryRequestProcessedOutput,
  IMarkDeliveryRequestProcessedHandler,
} from "./u/mark-delivery-request-processed.js";

export type {
  UpdateDeliveryAttemptStatusInput,
  UpdateDeliveryAttemptStatusOutput,
  IUpdateDeliveryAttemptStatusHandler,
} from "./u/update-delivery-attempt-status.js";
export { UPDATE_DELIVERY_ATTEMPT_STATUS_REDACTION } from "./u/update-delivery-attempt-status.js";

export type {
  UpdateChannelPreferenceRecordInput,
  UpdateChannelPreferenceRecordOutput,
  IUpdateChannelPreferenceRecordHandler,
} from "./u/update-channel-preference-record.js";

// --- Delete (D) ---
export type {
  PurgeDeletedMessagesInput,
  PurgeDeletedMessagesOutput,
  IPurgeDeletedMessagesHandler,
} from "./d/purge-deleted-messages.js";

export type {
  PurgeDeliveryHistoryInput,
  PurgeDeliveryHistoryOutput,
  IPurgeDeliveryHistoryHandler,
} from "./d/purge-delivery-history.js";

// --- Read (R) ---
export type { PingDbInput, PingDbOutput, IPingDbHandler } from "./r/ping-db.js";

// ---------------------------------------------------------------------------
// Bundle types — one per aggregate, used by app-layer factory functions
// ---------------------------------------------------------------------------

export interface MessageRepoHandlers {
  create: ICreateMessageRecordHandler;
  findById: IFindMessageByIdHandler;
}

export interface DeliveryRequestRepoHandlers {
  create: ICreateDeliveryRequestRecordHandler;
  findById: IFindDeliveryRequestByIdHandler;
  findByCorrelationId: IFindDeliveryRequestByCorrelationIdHandler;
  markProcessed: IMarkDeliveryRequestProcessedHandler;
}

export interface DeliveryAttemptRepoHandlers {
  create: ICreateDeliveryAttemptRecordHandler;
  findByRequestId: IFindDeliveryAttemptsByRequestIdHandler;
  updateStatus: IUpdateDeliveryAttemptStatusHandler;
}

export interface ChannelPreferenceRepoHandlers {
  create: ICreateChannelPreferenceRecordHandler;
  findByContactId: IFindChannelPreferenceByContactIdHandler;
  update: IUpdateChannelPreferenceRecordHandler;
}
