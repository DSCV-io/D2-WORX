import * as grpc from "@grpc/grpc-js";
import type { FileCallbackServer } from "@d2/protos";
import type { ServiceProvider } from "@d2/di";
import type { ILogger } from "@d2/logging";
import { createRpcScope, withTraceContext } from "@d2/service-defaults/grpc";
import { d2ResultToProto } from "@d2/result-extensions";
import { IHandleFileProcessedKey } from "@d2/auth-app";

/**
 * Creates the FileCallbackServer implementation.
 * Called BY the Files service when file processing completes.
 *
 * Each RPC creates a DI scope for traceId isolation and fresh handler instances.
 */
export function createFileCallbackGrpcService(
  provider: ServiceProvider,
  logger: ILogger,
): FileCallbackServer {
  return {
    onFileProcessed: (call, callback) => {
      return withTraceContext(call, async () => {
        const scope = createRpcScope(provider, call);
        try {
          const { fileId, contextKey, relatedEntityId, status } = call.request;
          if (!fileId || !contextKey || !relatedEntityId || !status) {
            callback({
              code: grpc.status.INVALID_ARGUMENT,
              message: "fileId, contextKey, relatedEntityId, and status are required.",
            });
            return;
          }
          const handler = scope.resolve(IHandleFileProcessedKey);
          const result = await handler.handleAsync({
            fileId,
            contextKey,
            relatedEntityId,
            status: status as "ready" | "rejected",
            variants: call.request.variants,
          });

          callback(null, {
            result: d2ResultToProto(result),
            success: result.success && (result.data?.success ?? false),
          });
        } catch (err: unknown) {
          logger.error("onFileProcessed RPC failed", {
            error: err instanceof Error ? err.message : String(err),
          });
          callback({
            code: grpc.status.INTERNAL,
            message: "Internal error",
          });
        } finally {
          scope.dispose();
        }
      });
    },

    canAccess: (call, callback) => {
      return withTraceContext(call, async () => {
        // Auth-owned context keys (user_avatar, org_logo, org_document) use JWT resolution,
        // so CanAccess is never called for them by the Files service. Fail-closed for safety.
        callback(null, {
          result: {
            success: true,
            statusCode: 200,
            messages: [],
            inputErrors: [],
            errorCode: "",
            traceId: "",
          },
          allowed: false,
        });
      });
    },
  };
}
