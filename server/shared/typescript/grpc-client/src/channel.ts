// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { Channel, ChannelCredentials } from "@grpc/grpc-js";
import { falsey } from "@d2/utilities";

/**
 * Options accepted by `getChannel`. Production callers pass nothing and
 * rely on env-var resolution; tests pass `endpoint` to point at an
 * in-process gRPC server.
 */
export interface GetChannelOptions {
  /** Override the endpoint URL — `host:port` form. */
  readonly endpoint?: string;
  /** Override the channel credentials (default: TLS). */
  readonly credentials?: ChannelCredentials;
  /** Whether to use insecure (plaintext) credentials — defaults to false. Tests use this. */
  readonly insecure?: boolean;
}

/**
 * Default channel options applied to every channel built here.
 * Mirrors the .NET `services.AddGrpcClient<T>().ConfigureChannel(...)`
 * shape: max message size 4MB, keepalive 10s.
 */
const DEFAULT_CHANNEL_OPTIONS: Record<string, number | string> = {
  "grpc.max_send_message_length": 4 * 1024 * 1024,
  "grpc.max_receive_message_length": 4 * 1024 * 1024,
  "grpc.keepalive_time_ms": 10_000,
  "grpc.keepalive_timeout_ms": 5_000,
};

let _channel: Channel | null = null;
let _channelInit: Promise<Channel> | null = null;

/**
 * Singleton-per-process gRPC channel accessor. Mirrors .NET
 * `services.AddGrpcClient<T>()` which registers a single channel per
 * client type — the BFF only ever talks to Edge, so one channel suffices.
 *
 * Concurrent first-call dedup: 100 simultaneous calls share one Promise;
 * resolved Promise persists in module scope. `closeChannel()` resets the
 * Promise so a future call re-initializes.
 */
export function getChannel(opts: GetChannelOptions = {}): Promise<Channel> {
  // Cached channel after first successful init.
  if (_channel !== null) return Promise.resolve(_channel);
  // Pending init promise — second + Nth concurrent caller share the
  // same Promise rather than racing to create separate channels.
  const inflight = _channelInit;
  if (inflight !== null) return inflight;
  const promise = _initChannel(opts);
  _channelInit = promise;
  return promise;
}

async function _initChannel(opts: GetChannelOptions): Promise<Channel> {
  // Yield once so concurrent callers in the same microtask all observe the
  // pending Promise (vs racing through full sync init each).
  await Promise.resolve();
  const endpoint = opts.endpoint ?? process.env["D2_EDGE_GRPC_ENDPOINT"];
  if (falsey(endpoint)) {
    _channelInit = null;
    throw new Error(
      "@d2/grpc-client: D2_EDGE_GRPC_ENDPOINT env var (or opts.endpoint) required",
    );
  }
  const credentials =
    opts.credentials ??
    (opts.insecure === true
      ? ChannelCredentials.createInsecure()
      : ChannelCredentials.createSsl());
  // `Channel` is the public alias for `ChannelImplementation`
  // (exported as `Channel as ChannelInterface` + `ChannelImplementation as Channel`).
  const channel = new (Channel as unknown as new (
    target: string,
    creds: ChannelCredentials,
    options: Record<string, number | string>,
  ) => Channel)(endpoint as string, credentials, DEFAULT_CHANNEL_OPTIONS);
  _channel = channel;
  return channel;
}

/**
 * Idempotent shutdown — closes the cached channel and resets module
 * state so a future `getChannel()` re-initializes. Safe to call from
 * any number of shutdown handlers; safe to call before any successful
 * `getChannel()`.
 */
export async function closeChannel(): Promise<void> {
  const c = _channel;
  _channel = null;
  _channelInit = null;
  if (c !== null) c.close();
}
