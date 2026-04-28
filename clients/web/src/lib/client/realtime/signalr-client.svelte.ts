/**
 * SignalR real-time client for browser WebSocket connections.
 *
 * Wraps @microsoft/signalr with Svelte 5 runes for reactive connection state.
 * Connects to the SignalR Gateway's authenticated hub with JWT via query param.
 * Auto-reconnects with exponential backoff (built into @microsoft/signalr).
 *
 * Usage:
 *   const client = createRealtimeClient();
 *   client.connect(url, tokenFactory);
 *   const unsub = client.on("file:ready", (payload) => { ... });
 *   // later:
 *   unsub();
 *   client.disconnect();
 */
import { HubConnectionBuilder, type HubConnection } from "@microsoft/signalr";
import { SvelteMap, SvelteSet } from "svelte/reactivity";

export type ConnectionState = "disconnected" | "connecting" | "connected" | "reconnecting";

type EventHandler = (payload: unknown) => void;

export interface RealtimeClient {
  /** Current connection state (reactive via $state). */
  readonly state: ConnectionState;
  /** Connect to the SignalR hub. Idempotent — calling while connected is a no-op. */
  connect(url: string, tokenFactory: () => Promise<string>): void;
  /** Disconnect from the hub. */
  disconnect(): Promise<void>;
  /** Subscribe to a named event. Returns an unsubscribe function. */
  on(event: string, handler: EventHandler): () => void;
}

/**
 * Creates a new SignalR real-time client instance.
 *
 * The client listens for the `ReceiveEvent(event, payloadJson)` hub method
 * (defined by the SignalR Gateway) and dispatches to registered handlers
 * by event name.
 */
export function createRealtimeClient(): RealtimeClient {
  let connection: HubConnection | undefined;
  let state = $state<ConnectionState>("disconnected");
  const listeners = new SvelteMap<string, SvelteSet<EventHandler>>();

  function dispatch(event: string, payloadJson: string): void {
    console.debug(`[SignalR] event="${event}"`);
    const handlers = listeners.get(event);
    if (!handlers || handlers.size === 0) return;

    let payload: unknown;
    try {
      payload = JSON.parse(payloadJson);
    } catch {
      return;
    }

    for (const handler of handlers) {
      try {
        handler(payload);
      } catch {
        // Swallow handler errors — don't break dispatch loop
      }
    }
  }

  return {
    get state() {
      return state;
    },

    connect(url: string, tokenFactory: () => Promise<string>) {
      if (connection) return;

      connection = new HubConnectionBuilder()
        .withUrl(url, { accessTokenFactory: tokenFactory })
        .withAutomaticReconnect()
        .build();

      connection.on("ReceiveEvent", (event: string, payloadJson: string) => {
        dispatch(event, payloadJson);
      });

      connection.onreconnecting(() => {
        state = "reconnecting";
      });
      connection.onreconnected(() => {
        state = "connected";
      });
      connection.onclose(() => {
        state = "disconnected";
      });

      state = "connecting";
      connection.start().then(
        () => {
          state = "connected";
        },
        () => {
          state = "disconnected";
        },
      );
    },

    async disconnect() {
      if (!connection) return;
      const conn = connection;
      connection = undefined;
      state = "disconnected";
      try {
        await conn.stop();
      } catch {
        // Ignore stop errors during teardown
      }
    },

    on(event: string, handler: EventHandler): () => void {
      let handlers = listeners.get(event);
      if (!handlers) {
        handlers = new SvelteSet();
        listeners.set(event, handlers);
      }
      handlers.add(handler);

      return () => {
        handlers!.delete(handler);
        if (handlers!.size === 0) {
          listeners.delete(event);
        }
      };
    },
  };
}
