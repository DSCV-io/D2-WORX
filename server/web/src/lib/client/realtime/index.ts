/**
 * SignalR real-time context for Svelte component tree.
 *
 * The root layout creates the client and sets it via `setRealtimeContext()`.
 * Components access it via `getRealtimeContext()`.
 */
import { setContext, getContext } from "svelte";
import type { RealtimeClient } from "./signalr-client.svelte.js";

const CONTEXT_KEY = Symbol.for("d2-realtime");

export function setRealtimeContext(client: RealtimeClient): void {
  setContext(CONTEXT_KEY, client);
}

export function getRealtimeContext(): RealtimeClient {
  return getContext<RealtimeClient>(CONTEXT_KEY);
}

export type { RealtimeClient, ConnectionState } from "./signalr-client.svelte.js";
export { createRealtimeClient } from "./signalr-client.svelte.js";
