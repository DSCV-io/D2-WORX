import * as grpc from "@grpc/grpc-js";
import type { ServiceProvider } from "@d2/di";
import type { ILogger } from "@d2/logging";
import { FilesServiceService, FilesJobServiceService } from "@d2/protos";
import { withApiKeyAuth } from "@d2/service-defaults/grpc";
import { createFilesGrpcService } from "../services/files-grpc-service.js";
import { createFilesJobsGrpcService } from "../services/files-jobs-grpc-service.js";

export interface GrpcServerOptions {
  provider: ServiceProvider;
  grpcPort: number;
  filesApiKeys?: string[];
  logger: ILogger;
}

/**
 * Creates, configures, and binds the gRPC server for FilesService + FilesJobService.
 */
export async function buildGrpcServer(options: GrpcServerOptions): Promise<grpc.Server> {
  const { provider, grpcPort, filesApiKeys, logger } = options;

  const server = new grpc.Server();
  const grpcService = createFilesGrpcService(provider, logger);
  const jobsGrpcService = createFilesJobsGrpcService(provider, logger);
  const publicRpcs = new Set(["checkHealth"]);

  if (!filesApiKeys?.length) {
    throw new Error(
      "FILES_API_KEYS not configured. gRPC server requires at least one API key (fail-closed).",
    );
  }

  const validKeys = new Set(filesApiKeys);
  server.addService(
    FilesServiceService,
    withApiKeyAuth(grpcService, { validKeys, logger, exempt: publicRpcs }),
  );
  server.addService(FilesJobServiceService, withApiKeyAuth(jobsGrpcService, { validKeys, logger }));
  logger.info(`Files gRPC API key authentication enabled (${validKeys.size} key(s))`);

  await new Promise<void>((resolve, reject) => {
    server.bindAsync(`0.0.0.0:${grpcPort}`, grpc.ServerCredentials.createInsecure(), (err) => {
      if (err) {
        reject(err);
        return;
      }
      logger.info(`Files gRPC server listening on 0.0.0.0:${grpcPort}`);
      resolve();
    });
  });

  return server;
}
