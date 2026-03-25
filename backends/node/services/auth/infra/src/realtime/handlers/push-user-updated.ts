import * as grpc from "@grpc/grpc-js";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import type { D2Result } from "@d2/result";
import {
  RealtimeGatewayClientCtor,
  type RealtimeGatewayClient,
  type RealtimePushResponse,
} from "@d2/protos";
import { handleGrpcCall } from "@d2/result-extensions";
import { createApiKeyInterceptor, createTraceContextInterceptor } from "@d2/service-defaults/grpc";
import type {
  PushUserUpdatedInput as I,
  PushUserUpdatedOutput as O,
  IPushUserUpdated,
} from "@d2/auth-app";

const GRPC_TIMEOUT_MS = 10_000;

/**
 * Pushes a user:updated event to all connected browser sessions for a user
 * via the SignalR Gateway.
 *
 * Signals clients to refresh their cached session data. The payload is
 * intentionally minimal — clients call get-session?disableCookieCache=true
 * to fetch fresh data from the DB.
 */
export class PushUserUpdated extends BaseHandler<I, O> implements IPushUserUpdated {
  private readonly gatewayAddress: string;
  private readonly apiKey: string;
  private client: RealtimeGatewayClient | undefined;

  constructor(gatewayAddress: string, apiKey: string, context: IHandlerContext) {
    super(context);
    this.gatewayAddress = gatewayAddress;
    this.apiKey = apiKey;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const client = this.getOrCreateClient();
    const channel = `user:${input.userId}`;
    const payload = JSON.stringify({ userId: input.userId });

    return handleGrpcCall(
      () =>
        new Promise<RealtimePushResponse>((resolve, reject) => {
          client.pushToChannel(
            {
              channel,
              event: "user:updated",
              payloadJson: payload,
            },
            new grpc.Metadata(),
            { deadline: Date.now() + GRPC_TIMEOUT_MS },
            (err: grpc.ServiceError | null, res: RealtimePushResponse) => {
              if (err) reject(err);
              else resolve(res);
            },
          );
        }),
      (res) => res.result!,
      (res) => ({ delivered: res.delivered ?? false }),
    );
  }

  private getOrCreateClient(): RealtimeGatewayClient {
    if (!this.client) {
      this.client = new RealtimeGatewayClientCtor(
        this.gatewayAddress,
        grpc.credentials.createInsecure(),
        {
          interceptors: [createTraceContextInterceptor(), createApiKeyInterceptor(this.apiKey)],
        },
      ) as unknown as RealtimeGatewayClient;
    }
    return this.client;
  }
}
