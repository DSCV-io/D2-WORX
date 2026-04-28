import * as grpc from "@grpc/grpc-js";
import type { CommsServiceServer } from "@d2/protos";
import type { ServiceProvider } from "@d2/di";
import { createRpcScope, withTraceContext } from "@d2/service-defaults/grpc";
import {
  ICheckHealthKey,
  IGetChannelPreferenceKey,
  ISetChannelPreferenceKey,
  IGetUserChannelPreferenceKey,
  ISetUserChannelPreferenceKey,
  IFindDeliveryRequestByIdKey,
  IFindDeliveryAttemptsByRequestIdKey,
} from "@d2/comms-app";
import { D2Result } from "@d2/result";
import { d2ResultToProto } from "@d2/result-extensions";
import { channelPreferenceToProto } from "../mappers/channel-preference-mapper.js";
import { deliveryRequestToProto, deliveryAttemptToProto } from "../mappers/delivery-mapper.js";

/**
 * Creates the CommsServiceServer implementation.
 * Each RPC handler creates a DI scope, resolves its handler(s), and disposes when done.
 * This ensures per-RPC traceId isolation and fresh handler instances.
 */
export function createCommsGrpcService(provider: ServiceProvider): CommsServiceServer {
  return {
    // ---- Health Check ----

    checkHealth: (call, callback) => {
      return withTraceContext(call, async () => {
        const scope = createRpcScope(provider, call);
        try {
          const handler = scope.resolve(ICheckHealthKey);
          const result = await handler.handleAsync({});

          const components: Record<string, { status: string; latencyMs: string; error: string }> =
            {};
          if (result.data?.components) {
            for (const [key, comp] of Object.entries(result.data.components)) {
              components[key] = {
                status: comp.status,
                latencyMs: String(comp.latencyMs ?? 0),
                error: comp.error ?? "",
              };
            }
          }

          callback(null, {
            status: result.data?.status ?? "unhealthy",
            timestamp: new Date().toISOString(),
            components,
          });
        } catch (err) {
          callback({
            code: grpc.status.INTERNAL,
            message: err instanceof Error ? err.message : "Unknown error",
          });
        } finally {
          scope.dispose();
        }
      });
    },

    // ---- Stage A: Delivery Engine ----

    getChannelPreference: (call, callback) => {
      return withTraceContext(call, async () => {
        const scope = createRpcScope(provider, call);
        try {
          const contactId = call.request.contactId;
          if (!contactId) {
            callback({ code: grpc.status.INVALID_ARGUMENT, message: "contactId is required." });
            return;
          }
          const handler = scope.resolve(IGetChannelPreferenceKey);
          const result = await handler.handleAsync({
            contactId,
          });

          if (!result.success || !result.data?.pref) {
            callback(null, {
              result: d2ResultToProto(
                result.data?.pref ? result : D2Result.notFound({ traceId: result.traceId }),
              ),
              data: undefined,
            });
            return;
          }

          callback(null, {
            result: d2ResultToProto(result),
            data: channelPreferenceToProto(result.data.pref),
          });
        } catch (err) {
          callback({
            code: grpc.status.INTERNAL,
            message: err instanceof Error ? err.message : "Unknown error",
          });
        } finally {
          scope.dispose();
        }
      });
    },

    setChannelPreference: (call, callback) => {
      return withTraceContext(call, async () => {
        const scope = createRpcScope(provider, call);
        try {
          const req = call.request;
          if (!req.contactId) {
            callback({ code: grpc.status.INVALID_ARGUMENT, message: "contactId is required." });
            return;
          }
          const handler = scope.resolve(ISetChannelPreferenceKey);
          const result = await handler.handleAsync({
            contactId: req.contactId,
            emailEnabled: req.emailEnabled,
            smsEnabled: req.smsEnabled,
          });

          if (!result.success || !result.data) {
            callback(null, {
              result: d2ResultToProto(result),
              data: undefined,
            });
            return;
          }

          callback(null, {
            result: d2ResultToProto(result),
            data: channelPreferenceToProto(result.data.pref),
          });
        } catch (err) {
          callback({
            code: grpc.status.INTERNAL,
            message: err instanceof Error ? err.message : "Unknown error",
          });
        } finally {
          scope.dispose();
        }
      });
    },

    getUserChannelPreference: (call, callback) => {
      return withTraceContext(call, async () => {
        const scope = createRpcScope(provider, call);
        try {
          const { contextKey, relatedEntityId } = call.request;
          if (!contextKey || !relatedEntityId) {
            callback({
              code: grpc.status.INVALID_ARGUMENT,
              message: "contextKey and relatedEntityId are required.",
            });
            return;
          }
          const handler = scope.resolve(IGetUserChannelPreferenceKey);
          const result = await handler.handleAsync({ contextKey, relatedEntityId });

          if (!result.success) {
            callback(null, { result: d2ResultToProto(result), data: undefined });
            return;
          }

          callback(null, {
            result: d2ResultToProto(result),
            data: result.data?.pref ? channelPreferenceToProto(result.data.pref) : undefined,
          });
        } catch (err) {
          callback({
            code: grpc.status.INTERNAL,
            message: err instanceof Error ? err.message : "Unknown error",
          });
        } finally {
          scope.dispose();
        }
      });
    },

    setUserChannelPreference: (call, callback) => {
      return withTraceContext(call, async () => {
        const scope = createRpcScope(provider, call);
        try {
          const req = call.request;
          if (!req.contextKey || !req.relatedEntityId) {
            callback({
              code: grpc.status.INVALID_ARGUMENT,
              message: "contextKey and relatedEntityId are required.",
            });
            return;
          }
          const handler = scope.resolve(ISetUserChannelPreferenceKey);
          const result = await handler.handleAsync({
            contextKey: req.contextKey,
            relatedEntityId: req.relatedEntityId,
            emailEnabled: req.emailEnabled,
            smsEnabled: req.smsEnabled,
          });

          if (!result.success || !result.data) {
            callback(null, { result: d2ResultToProto(result), data: undefined });
            return;
          }

          callback(null, {
            result: d2ResultToProto(result),
            data: channelPreferenceToProto(result.data.pref),
          });
        } catch (err) {
          callback({
            code: grpc.status.INTERNAL,
            message: err instanceof Error ? err.message : "Unknown error",
          });
        } finally {
          scope.dispose();
        }
      });
    },

    getDeliveryStatus: (call, callback) => {
      return withTraceContext(call, async () => {
        const scope = createRpcScope(provider, call);
        try {
          const deliveryRequestId = call.request.deliveryRequestId;
          if (!deliveryRequestId) {
            callback({
              code: grpc.status.INVALID_ARGUMENT,
              message: "deliveryRequestId is required.",
            });
            return;
          }

          const findRequestById = scope.resolve(IFindDeliveryRequestByIdKey);
          const reqResult = await findRequestById.handleAsync({
            id: deliveryRequestId,
          });

          if (!reqResult.success || !reqResult.data?.request) {
            callback(null, {
              result: d2ResultToProto(D2Result.notFound({ traceId: reqResult.traceId })),
              request: undefined,
              attempts: [],
            });
            return;
          }

          const findAttemptsByRequestId = scope.resolve(IFindDeliveryAttemptsByRequestIdKey);
          const attemptsResult = await findAttemptsByRequestId.handleAsync({
            requestId: deliveryRequestId,
          });

          const attempts =
            attemptsResult.success && attemptsResult.data
              ? attemptsResult.data.attempts.map(deliveryAttemptToProto)
              : [];

          callback(null, {
            result: d2ResultToProto(D2Result.ok()),
            request: deliveryRequestToProto(reqResult.data.request),
            attempts,
          });
        } catch (err) {
          callback({
            code: grpc.status.INTERNAL,
            message: err instanceof Error ? err.message : "Unknown error",
          });
        } finally {
          scope.dispose();
        }
      });
    },

    // ---- Stage B: In-App Notifications (stubs) ----
    getNotifications: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },
    markNotificationsRead: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },

    // ---- Stage C: Conversational Messaging (stubs) ----
    createThread: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },
    getThread: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },
    getThreads: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },
    postMessage: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },
    editMessage: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },
    deleteMessage: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },
    getThreadMessages: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },
    addReaction: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },
    removeReaction: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },
    addParticipant: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },
    removeParticipant: (_call, cb) => {
      cb({ code: grpc.status.UNIMPLEMENTED, message: "Not implemented" });
    },
  };
}
