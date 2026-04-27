import * as grpc from "@grpc/grpc-js";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import type { D2Result } from "@d2/result";
import {
  FileCallbackClientCtor,
  type FileCallbackClient,
  type CanAccessResponse,
} from "@d2/protos";
import { handleGrpcCall } from "@d2/result-extensions";
import { createApiKeyInterceptor, createTraceContextInterceptor } from "@d2/service-defaults/grpc";
import {
  CALL_CAN_ACCESS_REDACTION,
  type CallCanAccessInput as I,
  type CallCanAccessOutput as O,
  type ICallCanAccess,
} from "@d2/files-app";
import type { FilesOutboundOptions } from "../../options.js";

/**
 * gRPC outbound client for CanAccess calls to owning services.
 *
 * Manages a connection cache keyed by address — creates a new gRPC channel
 * on first call to a given address, reuses thereafter.
 */
export class CallCanAccess extends BaseHandler<I, O> implements ICallCanAccess {
  private readonly clients: Map<string, FileCallbackClient>;
  private readonly apiKey: string;
  private readonly options: FilesOutboundOptions;

  constructor(
    clients: Map<string, FileCallbackClient>,
    apiKey: string,
    options: FilesOutboundOptions,
    context: IHandlerContext,
  ) {
    super(context);
    this.clients = clients;
    this.apiKey = apiKey;
    this.options = options;
  }

  override get redaction(): RedactionSpec {
    return CALL_CAN_ACCESS_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const client = this.getOrCreateClient(input.address);

    return handleGrpcCall(
      () =>
        new Promise<CanAccessResponse>((resolve, reject) => {
          client.canAccess(
            {
              contextKey: input.contextKey,
              relatedEntityId: input.relatedEntityId,
              requestingUserId: input.requestingUserId,
              requestingOrgId: input.requestingOrgId,
              action: input.action,
            },
            new grpc.Metadata(),
            { deadline: Date.now() + this.options.grpcTimeoutMs },
            (err, res) => {
              if (err) reject(err);
              else resolve(res);
            },
          );
        }),
      (res) => res.result!,
      (res) => ({ allowed: res.allowed ?? false }),
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
