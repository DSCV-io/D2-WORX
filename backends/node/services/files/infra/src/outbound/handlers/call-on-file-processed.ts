import * as grpc from "@grpc/grpc-js";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import type { D2Result } from "@d2/result";
import {
  FileCallbackClientCtor,
  type FileCallbackClient,
  type FileProcessedResponse,
} from "@d2/protos";
import { handleGrpcCall } from "@d2/result-extensions";
import { createApiKeyInterceptor, createTraceContextInterceptor } from "@d2/service-defaults/grpc";
import {
  CALL_ON_FILE_PROCESSED_REDACTION,
  type CallOnFileProcessedInput as I,
  type CallOnFileProcessedOutput as O,
  type ICallOnFileProcessed,
} from "@d2/files-app";

const GRPC_TIMEOUT_MS = 10_000;

/**
 * gRPC outbound client for OnFileProcessed calls to owning services.
 *
 * Manages a connection cache keyed by address — creates a new gRPC channel
 * on first call to a given address, reuses thereafter.
 */
export class CallOnFileProcessed extends BaseHandler<I, O> implements ICallOnFileProcessed {
  private readonly clients: Map<string, FileCallbackClient>;
  private readonly apiKey: string;

  constructor(clients: Map<string, FileCallbackClient>, apiKey: string, context: IHandlerContext) {
    super(context);
    this.clients = clients;
    this.apiKey = apiKey;
  }

  override get redaction(): RedactionSpec {
    return CALL_ON_FILE_PROCESSED_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const client = this.getOrCreateClient(input.address);

    return handleGrpcCall(
      () =>
        new Promise<FileProcessedResponse>((resolve, reject) => {
          client.onFileProcessed(
            {
              fileId: input.fileId,
              contextKey: input.contextKey,
              relatedEntityId: input.relatedEntityId,
              status: input.status,
              variants: input.variants ? [...input.variants] : [],
            },
            new grpc.Metadata(),
            { deadline: Date.now() + GRPC_TIMEOUT_MS },
            (err, res) => {
              if (err) reject(err);
              else resolve(res);
            },
          );
        }),
      (res) => res.result!,
      (res) => ({ success: res.success ?? false }),
    );
  }

  private getOrCreateClient(address: string): FileCallbackClient {
    let client = this.clients.get(address);
    if (!client) {
      client = new FileCallbackClientCtor(address, grpc.credentials.createInsecure(), {
        interceptors: [createTraceContextInterceptor(), createApiKeyInterceptor(this.apiKey)],
      }) as unknown as FileCallbackClient;
      this.clients.set(address, client);
    }
    return client;
  }
}
