// @d2/comms-app — CQRS handlers for the Comms service delivery engine.
// Zero infra imports — this package is pure application logic.

export { COMMS_CACHE_KEYS } from "./cache-keys.js";

// --- CQRS Handler Interfaces (app-layer contracts) ---
export {
  Commands as CommsCommands,
  Queries as CommsQueries,
  Complex as CommsComplex,
} from "./interfaces/cqrs/handlers/index.js";

import type { IHandlerContext } from "@d2/handler";
import type { ChannelPreference } from "@d2/comms-domain";
import type { InMemoryCache } from "@d2/interfaces";
import type { Queries } from "@d2/geo-client";

// --- Interfaces (Provider Contracts) ---
export type {
  IEmailProvider,
  SendEmailInput,
  SendEmailOutput,
  ISmsProvider,
  SendSmsInput,
  SendSmsOutput,
} from "./interfaces/providers/index.js";

// --- Interfaces (Repository Handler Bundles) ---
export type {
  // Bundle types (used by factory functions + composition root)
  MessageRepoHandlers,
  DeliveryRequestRepoHandlers,
  DeliveryAttemptRepoHandlers,
  ChannelPreferenceRepoHandlers,
  // Individual handler types
  ICreateMessageRecordHandler,
  ICreateDeliveryRequestRecordHandler,
  ICreateDeliveryAttemptRecordHandler,
  ICreateChannelPreferenceRecordHandler,
  IFindMessageByIdHandler,
  IFindDeliveryRequestByIdHandler,
  IFindDeliveryRequestByCorrelationIdHandler,
  IFindDeliveryAttemptsByRequestIdHandler,
  IFindChannelPreferenceByContactIdHandler,
  IMarkDeliveryRequestProcessedHandler,
  IUpdateDeliveryAttemptStatusHandler,
  IUpdateChannelPreferenceRecordHandler,
  // Individual I/O types
  CreateMessageRecordInput,
  CreateMessageRecordOutput,
  CreateDeliveryRequestRecordInput,
  CreateDeliveryRequestRecordOutput,
  CreateDeliveryAttemptRecordInput,
  CreateDeliveryAttemptRecordOutput,
  CreateChannelPreferenceRecordInput,
  CreateChannelPreferenceRecordOutput,
  FindMessageByIdInput,
  FindMessageByIdOutput,
  FindDeliveryRequestByIdInput,
  FindDeliveryRequestByIdOutput,
  FindDeliveryRequestByCorrelationIdInput,
  FindDeliveryRequestByCorrelationIdOutput,
  FindDeliveryAttemptsByRequestIdInput,
  FindDeliveryAttemptsByRequestIdOutput,
  FindChannelPreferenceByContactIdInput,
  FindChannelPreferenceByContactIdOutput,
  MarkDeliveryRequestProcessedInput,
  MarkDeliveryRequestProcessedOutput,
  UpdateDeliveryAttemptStatusInput,
  UpdateDeliveryAttemptStatusOutput,
  UpdateChannelPreferenceRecordInput,
  UpdateChannelPreferenceRecordOutput,
  // Delete (D) — Purge handlers
  PurgeDeletedMessagesInput,
  PurgeDeletedMessagesOutput,
  IPurgeDeletedMessagesHandler,
  PurgeDeliveryHistoryInput,
  PurgeDeliveryHistoryOutput,
  IPurgeDeliveryHistoryHandler,
  // Read (R) — PingDb
  PingDbInput,
  PingDbOutput,
  IPingDbHandler,
} from "./interfaces/repository/handlers/index.js";

// --- Complex Handlers ---
export { Deliver } from "./implementations/cqrs/handlers/x/deliver.js";
export type { DeliverInput, DeliverOutput } from "./interfaces/cqrs/handlers/x/deliver.js";

// --- Channel Dispatchers ---
export {
  EmailDispatcher,
  SmsDispatcher,
  createEmailWrapper,
  loadEmailWrapper,
} from "./implementations/cqrs/handlers/x/channel-dispatchers.js";
export type {
  IChannelDispatcher,
  DispatchInput,
  DispatchResult,
} from "./implementations/cqrs/handlers/x/channel-dispatchers.js";

// --- Command Handlers ---
export { SetChannelPreference } from "./implementations/cqrs/handlers/c/set-channel-preference.js";
export type {
  SetChannelPreferenceInput,
  SetChannelPreferenceOutput,
} from "./interfaces/cqrs/handlers/c/set-channel-preference.js";

export { SetUserChannelPreference } from "./implementations/cqrs/handlers/c/set-user-channel-preference.js";
export type {
  SetUserChannelPreferenceInput,
  SetUserChannelPreferenceOutput,
  ISetUserChannelPreferenceHandler,
} from "./interfaces/cqrs/handlers/c/set-user-channel-preference.js";

// --- Job Handlers (Command) ---
export { RunDeletedMessagePurge } from "./implementations/cqrs/handlers/c/run-deleted-message-purge.js";
export type {
  RunDeletedMessagePurgeInput,
  RunDeletedMessagePurgeOutput,
} from "./interfaces/cqrs/handlers/c/run-deleted-message-purge.js";

export { RunDeliveryHistoryPurge } from "./implementations/cqrs/handlers/c/run-delivery-history-purge.js";
export type {
  RunDeliveryHistoryPurgeInput,
  RunDeliveryHistoryPurgeOutput,
} from "./interfaces/cqrs/handlers/c/run-delivery-history-purge.js";

// --- Job Options ---
export type { CommsJobOptions } from "./comms-job-options.js";
export { DEFAULT_COMMS_JOB_OPTIONS } from "./comms-job-options.js";

// --- Query Handlers ---
export { RecipientResolver } from "./implementations/cqrs/handlers/q/resolve-recipient.js";
export type {
  ResolveRecipientInput,
  ResolveRecipientOutput,
} from "./interfaces/cqrs/handlers/q/resolve-recipient.js";

export { GetChannelPreference } from "./implementations/cqrs/handlers/q/get-channel-preference.js";
export type {
  GetChannelPreferenceInput,
  GetChannelPreferenceOutput,
} from "./interfaces/cqrs/handlers/q/get-channel-preference.js";

export { GetUserChannelPreference } from "./implementations/cqrs/handlers/q/get-user-channel-preference.js";
export type {
  GetUserChannelPreferenceInput,
  GetUserChannelPreferenceOutput,
  IGetUserChannelPreferenceHandler,
} from "./interfaces/cqrs/handlers/q/get-user-channel-preference.js";

// ---------------------------------------------------------------------------
// Factory Functions
// ---------------------------------------------------------------------------

import type {
  MessageRepoHandlers,
  DeliveryRequestRepoHandlers,
  DeliveryAttemptRepoHandlers,
  ChannelPreferenceRepoHandlers,
} from "./interfaces/repository/handlers/index.js";
import type { IEmailProvider } from "./interfaces/providers/index.js";
import type { ISmsProvider } from "./interfaces/providers/index.js";
import { Deliver } from "./implementations/cqrs/handlers/x/deliver.js";
import {
  EmailDispatcher,
  SmsDispatcher,
} from "./implementations/cqrs/handlers/x/channel-dispatchers.js";
import type { IChannelDispatcher } from "./implementations/cqrs/handlers/x/channel-dispatchers.js";
import { RecipientResolver } from "./implementations/cqrs/handlers/q/resolve-recipient.js";
import { SetChannelPreference } from "./implementations/cqrs/handlers/c/set-channel-preference.js";
import { GetChannelPreference } from "./implementations/cqrs/handlers/q/get-channel-preference.js";

/** Creates the delivery engine handlers (orchestrator + CRUD). */
export function createDeliveryHandlers(
  repos: {
    message: MessageRepoHandlers;
    request: DeliveryRequestRepoHandlers;
    attempt: DeliveryAttemptRepoHandlers;
    channelPref: ChannelPreferenceRepoHandlers;
  },
  providers: { email: IEmailProvider; sms?: ISmsProvider },
  getContactsByIds: Queries.IGetContactsByIdsHandler,
  context: IHandlerContext,
  cache?: {
    channelPref?: {
      get: InMemoryCache.IGetHandler<ChannelPreference>;
      set: InMemoryCache.ISetHandler<ChannelPreference>;
    };
  },
  options?: { emailWrapper?: string },
): DeliveryHandlers {
  const recipientResolver = new RecipientResolver(getContactsByIds, context);

  const dispatchers: IChannelDispatcher[] = [
    new EmailDispatcher(providers.email, options?.emailWrapper),
  ];
  if (providers.sms) {
    dispatchers.push(new SmsDispatcher(providers.sms));
  }

  const deliver = new Deliver(repos, dispatchers, recipientResolver, context);

  return {
    deliver,
    resolveRecipient: recipientResolver,
    setChannelPreference: new SetChannelPreference(repos.channelPref, context, cache?.channelPref),
    getChannelPreference: new GetChannelPreference(repos.channelPref, context, cache?.channelPref),
  };
}

/** Return type of createDeliveryHandlers. */
export type DeliveryHandlers = {
  deliver: Deliver;
  resolveRecipient: RecipientResolver;
  setChannelPreference: SetChannelPreference;
  getChannelPreference: GetChannelPreference;
};

// --- Health Check Handler ---
export { CheckHealth } from "./implementations/cqrs/handlers/q/check-health.js";
export type {
  CheckHealthInput,
  CheckHealthOutput,
  ComponentHealth,
} from "./interfaces/cqrs/handlers/q/check-health.js";

// --- DI Registration ---
export { addCommsApp } from "./registration.js";
export {
  // Infra keys
  ICreateMessageRecordKey,
  IFindMessageByIdKey,
  ICreateDeliveryRequestRecordKey,
  IFindDeliveryRequestByIdKey,
  IFindDeliveryRequestByCorrelationIdKey,
  IMarkDeliveryRequestProcessedKey,
  ICreateDeliveryAttemptRecordKey,
  IFindDeliveryAttemptsByRequestIdKey,
  IUpdateDeliveryAttemptStatusKey,
  ICreateChannelPreferenceRecordKey,
  IFindChannelPreferenceByContactIdKey,
  IUpdateChannelPreferenceRecordKey,
  IEmailProviderKey,
  ISmsProviderKey,
  // App keys
  IDeliverKey,
  IRecipientResolverKey,
  ISetChannelPreferenceKey,
  ISetUserChannelPreferenceKey,
  IGetChannelPreferenceKey,
  IGetUserChannelPreferenceKey,
  IPingDbKey,
  ICheckHealthKey,
  IPurgeDeletedMessagesKey,
  IPurgeDeliveryHistoryKey,
  IRunDeletedMessagePurgeKey,
  IRunDeliveryHistoryPurgeKey,
} from "./service-keys.js";
