import { createServiceKey } from "@d2/di";

// Import interface types for infra-level keys
import type { IPingDbHandler } from "./interfaces/repository/handlers/index.js";
import type {
  ICreateMessageRecordHandler,
  IFindMessageByIdHandler,
  ICreateDeliveryRequestRecordHandler,
  IFindDeliveryRequestByIdHandler,
  IFindDeliveryRequestByCorrelationIdHandler,
  IMarkDeliveryRequestProcessedHandler,
  ICreateDeliveryAttemptRecordHandler,
  IFindDeliveryAttemptsByRequestIdHandler,
  IUpdateDeliveryAttemptStatusHandler,
  ICreateChannelPreferenceRecordHandler,
  IFindChannelPreferenceByContactIdHandler,
  IUpdateChannelPreferenceRecordHandler,
} from "./interfaces/repository/handlers/index.js";
import type { IEmailProvider } from "./interfaces/providers/email/index.js";
import type { ISmsProvider } from "./interfaces/providers/sms/index.js";
import type {
  IPurgeDeletedMessagesHandler,
  IPurgeDeliveryHistoryHandler,
} from "./interfaces/repository/handlers/index.js";

// Import app-layer handler interfaces
import type { Commands, Queries, Complex } from "./interfaces/cqrs/handlers/index.js";

// =============================================================================
// Infrastructure-layer keys (interfaces defined here, implemented in comms-infra)
// =============================================================================

// --- Message Repository ---
export const ICreateMessageRecordKey = createServiceKey<ICreateMessageRecordHandler>(
  "Comms.Repo.CreateMessageRecord",
);
export const IFindMessageByIdKey = createServiceKey<IFindMessageByIdHandler>(
  "Comms.Repo.FindMessageById",
);

// --- Delivery Request Repository ---
export const ICreateDeliveryRequestRecordKey =
  createServiceKey<ICreateDeliveryRequestRecordHandler>("Comms.Repo.CreateDeliveryRequestRecord");
export const IFindDeliveryRequestByIdKey = createServiceKey<IFindDeliveryRequestByIdHandler>(
  "Comms.Repo.FindDeliveryRequestById",
);
export const IFindDeliveryRequestByCorrelationIdKey =
  createServiceKey<IFindDeliveryRequestByCorrelationIdHandler>(
    "Comms.Repo.FindDeliveryRequestByCorrelationId",
  );
export const IMarkDeliveryRequestProcessedKey =
  createServiceKey<IMarkDeliveryRequestProcessedHandler>("Comms.Repo.MarkDeliveryRequestProcessed");

// --- Delivery Attempt Repository ---
export const ICreateDeliveryAttemptRecordKey =
  createServiceKey<ICreateDeliveryAttemptRecordHandler>("Comms.Repo.CreateDeliveryAttemptRecord");
export const IFindDeliveryAttemptsByRequestIdKey =
  createServiceKey<IFindDeliveryAttemptsByRequestIdHandler>(
    "Comms.Repo.FindDeliveryAttemptsByRequestId",
  );
export const IUpdateDeliveryAttemptStatusKey =
  createServiceKey<IUpdateDeliveryAttemptStatusHandler>("Comms.Repo.UpdateDeliveryAttemptStatus");

// --- Channel Preference Repository ---
export const ICreateChannelPreferenceRecordKey =
  createServiceKey<ICreateChannelPreferenceRecordHandler>(
    "Comms.Repo.CreateChannelPreferenceRecord",
  );
export const IFindChannelPreferenceByContactIdKey =
  createServiceKey<IFindChannelPreferenceByContactIdHandler>(
    "Comms.Repo.FindChannelPreferenceByContactId",
  );
export const IUpdateChannelPreferenceRecordKey =
  createServiceKey<IUpdateChannelPreferenceRecordHandler>(
    "Comms.Repo.UpdateChannelPreferenceRecord",
  );

// --- Health Check Repository Handler ---
export const IPingDbKey = createServiceKey<IPingDbHandler>("Comms.Repo.PingDb");

// --- Job Repository ---
export const IPurgeDeletedMessagesKey = createServiceKey<IPurgeDeletedMessagesHandler>(
  "Comms.Repo.PurgeDeletedMessages",
);
export const IPurgeDeliveryHistoryKey = createServiceKey<IPurgeDeliveryHistoryHandler>(
  "Comms.Repo.PurgeDeliveryHistory",
);

// --- Providers ---
export const IEmailProviderKey = createServiceKey<IEmailProvider>("Comms.Infra.EmailProvider");
export const ISmsProviderKey = createServiceKey<ISmsProvider>("Comms.Infra.SmsProvider");

// =============================================================================
// Application-layer keys (defined and implemented in comms-app)
// =============================================================================

// --- Complex Handlers ---
export const IDeliverKey = createServiceKey<Complex.IDeliverHandler>("Comms.App.Deliver");
export const IRecipientResolverKey = createServiceKey<Queries.IRecipientResolverHandler>(
  "Comms.App.RecipientResolver",
);

// --- Command Handlers ---
export const ISetChannelPreferenceKey = createServiceKey<Commands.ISetChannelPreferenceHandler>(
  "Comms.App.SetChannelPreference",
);
export const ISetUserChannelPreferenceKey =
  createServiceKey<Commands.ISetUserChannelPreferenceHandler>("Comms.App.SetUserChannelPreference");

// --- Query Handlers ---
export const IGetChannelPreferenceKey = createServiceKey<Queries.IGetChannelPreferenceHandler>(
  "Comms.App.GetChannelPreference",
);
export const IGetUserChannelPreferenceKey =
  createServiceKey<Queries.IGetUserChannelPreferenceHandler>("Comms.App.GetUserChannelPreference");
export const ICheckHealthKey =
  createServiceKey<Queries.ICheckHealthHandler>("Comms.App.CheckHealth");

// --- Job Handlers (Command) ---
export const IRunDeletedMessagePurgeKey = createServiceKey<Commands.IRunDeletedMessagePurgeHandler>(
  "Comms.App.RunDeletedMessagePurge",
);
export const IRunDeliveryHistoryPurgeKey =
  createServiceKey<Commands.IRunDeliveryHistoryPurgeHandler>("Comms.App.RunDeliveryHistoryPurge");
