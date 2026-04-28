import * as grpc from "@grpc/grpc-js";
import type { RealtimeGatewayServer, PushToChannelRequest } from "@d2/protos";
import { RealtimeGatewayService, D2ResultProtoFns } from "@d2/protos";

export interface CapturedPushEvent {
  channel: string;
  event: string;
  payloadJson: string;
}

let server: grpc.Server | undefined;
let address: string;
const capturedEvents: CapturedPushEvent[] = [];

/**
 * Stub gRPC server implementing RealtimeGatewayService.
 *
 * - `pushToChannel`: captures `{ channel, event, payloadJson }` in array, returns success
 * - `removeFromChannel`: no-op, returns success
 * - `checkHealth`: returns healthy
 */
function createStubService(): RealtimeGatewayServer {
  return {
    pushToChannel: (call, callback) => {
      const req: PushToChannelRequest = call.request;
      capturedEvents.push({
        channel: req.channel,
        event: req.event,
        payloadJson: req.payloadJson,
      });

      callback(null, {
        result: D2ResultProtoFns.fromPartial({
          success: true,
          statusCode: 200,
          messages: [],
          inputErrors: [],
          errorCode: "",
          traceId: "",
        }),
        delivered: true,
        connectionsReached: 1,
      });
    },
    removeFromChannel: (_call, callback) => {
      callback(null, {
        result: D2ResultProtoFns.fromPartial({
          success: true,
          statusCode: 200,
          messages: [],
          inputErrors: [],
          errorCode: "",
          traceId: "",
        }),
        delivered: true,
        connectionsReached: 0,
      });
    },
    checkHealth: (_call, callback) => {
      callback(null, {
        status: "healthy",
        timestamp: new Date().toISOString(),
        components: {},
      });
    },
  };
}

/**
 * Starts the stub SignalR gateway gRPC server on an ephemeral port.
 */
export async function startStubSignalR(): Promise<void> {
  server = new grpc.Server();
  server.addService(RealtimeGatewayService, createStubService());

  const port = await new Promise<number>((resolve, reject) => {
    server!.bindAsync("0.0.0.0:0", grpc.ServerCredentials.createInsecure(), (err, boundPort) => {
      if (err) {
        reject(err);
        return;
      }
      resolve(boundPort);
    });
  });

  address = `localhost:${port}`;
}

/**
 * Stops the stub gRPC server.
 */
export async function stopStubSignalR(): Promise<void> {
  if (!server) return;
  await new Promise<void>((resolve) => {
    const timeout = setTimeout(() => {
      server?.forceShutdown();
      resolve();
    }, 3_000);
    server!.tryShutdown(() => {
      clearTimeout(timeout);
      resolve();
    });
  });
  server = undefined;
}

/** gRPC address of the stub server (e.g., "localhost:54321"). */
export function getStubSignalRAddress(): string {
  return address;
}

/** Returns all captured push events (readonly snapshot). */
export function getCapturedPushEvents(): readonly CapturedPushEvent[] {
  return [...capturedEvents];
}

/** Clears captured push events (call between tests). */
export function clearCapturedPushEvents(): void {
  capturedEvents.length = 0;
}
