import type { ServiceCollection } from "@d2/di";
import { IHandlerContextKey } from "@d2/handler";
import { IGetContactsByExtKeysKey, IGetContactsByIdsKey } from "@d2/geo-client";
import {
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
  IPurgeDeletedMessagesKey,
  IPurgeDeliveryHistoryKey,
  // App keys
  IDeliverKey,
  IRecipientResolverKey,
  ISetChannelPreferenceKey,
  ISetUserChannelPreferenceKey,
  IGetChannelPreferenceKey,
  IGetUserChannelPreferenceKey,
  IPingDbKey,
  ICheckHealthKey,
  IRunDeletedMessagePurgeKey,
  IRunDeliveryHistoryPurgeKey,
} from "./service-keys.js";
import { Deliver } from "./implementations/cqrs/handlers/x/deliver.js";
import {
  EmailDispatcher,
  SmsDispatcher,
  loadEmailWrapper,
} from "./implementations/cqrs/handlers/x/channel-dispatchers.js";
import type { IChannelDispatcher } from "./implementations/cqrs/handlers/x/channel-dispatchers.js";
import { RecipientResolver } from "./implementations/cqrs/handlers/q/resolve-recipient.js";
import { SetChannelPreference } from "./implementations/cqrs/handlers/c/set-channel-preference.js";
import { SetUserChannelPreference } from "./implementations/cqrs/handlers/c/set-user-channel-preference.js";
import { GetChannelPreference } from "./implementations/cqrs/handlers/q/get-channel-preference.js";
import { GetUserChannelPreference } from "./implementations/cqrs/handlers/q/get-user-channel-preference.js";
import { CheckHealth } from "./implementations/cqrs/handlers/q/check-health.js";
import { RunDeletedMessagePurge } from "./implementations/cqrs/handlers/c/run-deleted-message-purge.js";
import { RunDeliveryHistoryPurge } from "./implementations/cqrs/handlers/c/run-delivery-history-purge.js";
import { IMessageBusPingKey } from "@d2/messaging";
import { DistributedCache } from "@d2/interfaces";
import type { CommsJobOptions } from "./comms-job-options.js";
import { DEFAULT_COMMS_JOB_OPTIONS } from "./comms-job-options.js";

/**
 * Registers comms application-layer services (CQRS handlers)
 * with the DI container. Mirrors .NET's `services.AddCommsApp()` pattern.
 *
 * All CQRS handlers are transient — new instance per resolve.
 */
export function addCommsApp(
  services: ServiceCollection,
  jobOptions: CommsJobOptions = DEFAULT_COMMS_JOB_OPTIONS,
  emailFooterText?: string,
  emailTemplatePath?: string,
): void {
  // --- Complex Handlers ---

  services.addTransient(
    IRecipientResolverKey,
    (sp) => new RecipientResolver(sp.resolve(IGetContactsByIdsKey), sp.resolve(IHandlerContextKey)),
  );

  services.addTransient(IDeliverKey, (sp) => {
    const emailWrapper =
      emailFooterText || emailTemplatePath
        ? loadEmailWrapper(emailFooterText ?? "D2-WORX", emailTemplatePath)
        : undefined;
    const dispatchers: IChannelDispatcher[] = [
      new EmailDispatcher(sp.resolve(IEmailProviderKey), emailWrapper),
    ];
    const smsProvider = sp.tryResolve(ISmsProviderKey);
    if (smsProvider) {
      dispatchers.push(new SmsDispatcher(smsProvider));
    }

    return new Deliver(
      {
        message: {
          create: sp.resolve(ICreateMessageRecordKey),
          findById: sp.resolve(IFindMessageByIdKey),
        },
        request: {
          create: sp.resolve(ICreateDeliveryRequestRecordKey),
          findById: sp.resolve(IFindDeliveryRequestByIdKey),
          findByCorrelationId: sp.resolve(IFindDeliveryRequestByCorrelationIdKey),
          markProcessed: sp.resolve(IMarkDeliveryRequestProcessedKey),
        },
        attempt: {
          create: sp.resolve(ICreateDeliveryAttemptRecordKey),
          findByRequestId: sp.resolve(IFindDeliveryAttemptsByRequestIdKey),
          updateStatus: sp.resolve(IUpdateDeliveryAttemptStatusKey),
        },
        channelPref: {
          create: sp.resolve(ICreateChannelPreferenceRecordKey),
          findByContactId: sp.resolve(IFindChannelPreferenceByContactIdKey),
          update: sp.resolve(IUpdateChannelPreferenceRecordKey),
        },
      },
      dispatchers,
      sp.resolve(IRecipientResolverKey),
      sp.resolve(IHandlerContextKey),
    );
  });

  // --- Command Handlers ---

  services.addTransient(
    ISetChannelPreferenceKey,
    (sp) =>
      new SetChannelPreference(
        {
          create: sp.resolve(ICreateChannelPreferenceRecordKey),
          findByContactId: sp.resolve(IFindChannelPreferenceByContactIdKey),
          update: sp.resolve(IUpdateChannelPreferenceRecordKey),
        },
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    ISetUserChannelPreferenceKey,
    (sp) =>
      new SetUserChannelPreference(
        sp.resolve(IGetContactsByExtKeysKey),
        sp.resolve(ISetChannelPreferenceKey),
        sp.resolve(IHandlerContextKey),
      ),
  );

  // --- Query Handlers ---

  services.addTransient(
    IGetChannelPreferenceKey,
    (sp) =>
      new GetChannelPreference(
        {
          create: sp.resolve(ICreateChannelPreferenceRecordKey),
          findByContactId: sp.resolve(IFindChannelPreferenceByContactIdKey),
          update: sp.resolve(IUpdateChannelPreferenceRecordKey),
        },
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    IGetUserChannelPreferenceKey,
    (sp) =>
      new GetUserChannelPreference(
        sp.resolve(IGetContactsByExtKeysKey),
        sp.resolve(IGetChannelPreferenceKey),
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    ICheckHealthKey,
    (sp) =>
      new CheckHealth(
        sp.resolve(IPingDbKey),
        sp.resolve(IHandlerContextKey),
        sp.tryResolve(DistributedCache.IDistributedCachePingKey),
        sp.tryResolve(IMessageBusPingKey),
      ),
  );

  // --- Job Handlers (Command) ---

  services.addTransient(
    IRunDeletedMessagePurgeKey,
    (sp) =>
      new RunDeletedMessagePurge(
        sp.resolve(DistributedCache.IDistributedCacheAcquireLockKey),
        sp.resolve(DistributedCache.IDistributedCacheReleaseLockKey),
        sp.resolve(IPurgeDeletedMessagesKey),
        jobOptions,
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    IRunDeliveryHistoryPurgeKey,
    (sp) =>
      new RunDeliveryHistoryPurge(
        sp.resolve(DistributedCache.IDistributedCacheAcquireLockKey),
        sp.resolve(DistributedCache.IDistributedCacheReleaseLockKey),
        sp.resolve(IPurgeDeliveryHistoryKey),
        jobOptions,
        sp.resolve(IHandlerContextKey),
      ),
  );
}
