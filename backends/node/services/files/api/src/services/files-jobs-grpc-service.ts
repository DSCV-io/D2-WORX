import * as grpc from "@grpc/grpc-js";
import type { FilesJobServiceServer } from "@d2/protos";
import type { ServiceProvider } from "@d2/di";
import type { ILogger } from "@d2/logging";
import { createRpcScope, withTraceContext } from "@d2/service-defaults/grpc";
import { IRunCleanupKey } from "@d2/files-app";

/**
 * Creates the FilesJobServiceServer implementation.
 *
 * RunCleanup has a custom output shape (pendingCleaned/processingCleaned/rejectedCleaned)
 * that doesn't match the standard JobRpcOutput (rowsAffected), so we map
 * the sum of cleaned counts to rowsAffected.
 */
export function createFilesJobsGrpcService(
  provider: ServiceProvider,
  logger: ILogger,
): FilesJobServiceServer {
  return {
    runCleanup: (call, callback) => {
      return withTraceContext(call, async () => {
        const scope = createRpcScope(provider, call);
        try {
          const handler = scope.resolve(IRunCleanupKey);
          const result = await handler.handleAsync({});

          const totalCleaned =
            (result.data?.pendingCleaned ?? 0) +
            (result.data?.processingCleaned ?? 0) +
            (result.data?.rejectedCleaned ?? 0);

          callback(null, {
            result: {
              success: result.success,
              statusCode: result.statusCode,
              messages: [...(result.messages ?? [])],
              inputErrors: [],
              errorCode: "",
              traceId: "",
            },
            data: {
              jobName: "run-cleanup",
              rowsAffected: totalCleaned,
              durationMs: String(result.data?.durationMs ?? 0),
              lockAcquired: result.data?.lockAcquired ?? false,
              executedAt: new Date().toISOString(),
            },
          });
        } catch (err: unknown) {
          logger.error("runCleanup RPC failed", {
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
  };
}
