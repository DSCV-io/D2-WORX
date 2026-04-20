import type { MessageBus, IncomingMessage } from "@d2/messaging";
import { ConsumerResult } from "@d2/messaging";
import type { ILogger } from "@d2/logging";
import type { ServiceProvider, ServiceScope } from "@d2/di";
import type { Complex } from "@d2/geo-client";
import { isValidGuid } from "@d2/handler";
import { AUTH_MESSAGING } from "@d2/auth-domain";
import { IUpdateSignInEventWhoIsIdKey, IUpdateSessionWhoIsIdKey } from "@d2/auth-app";

export interface WhoIsResolutionConsumerDeps {
  messageBus: MessageBus;
  provider: ServiceProvider;
  createScope: (provider: ServiceProvider) => ServiceScope;
  findWhoIs: Complex.IFindWhoIsHandler;
  logger: ILogger;
}

interface WhoIsResolutionMessage {
  signInEventId: string;
  /** Optional — only present for successful sign-ins (failed attempts have no session). */
  sessionId?: string;
  ipAddress: string;
  userAgent: string;
}

function validateMessage(body: unknown): WhoIsResolutionMessage | null {
  if (typeof body !== "object" || body === null) return null;
  const obj = body as Record<string, unknown>;
  if (typeof obj.signInEventId !== "string" || !isValidGuid(obj.signInEventId)) return null;
  // sessionId is optional, but if present must be a non-empty string (BetterAuth uses opaque IDs, not GUIDs)
  if (obj.sessionId !== undefined) {
    if (typeof obj.sessionId !== "string" || obj.sessionId.length === 0 || obj.sessionId.length > 255)
      return null;
  }
  if (typeof obj.ipAddress !== "string" || obj.ipAddress.length === 0 || obj.ipAddress.length > 45)
    return null;
  if (
    typeof obj.userAgent !== "string" ||
    obj.userAgent.length === 0 ||
    obj.userAgent.length > 2000
  )
    return null;
  return obj as unknown as WhoIsResolutionMessage;
}

/**
 * Creates a RabbitMQ consumer for async WhoIs resolution of sign-in events.
 *
 * Auth self-consume pattern: the onSignIn hook publishes a message after recording
 * the sign-in event, and this consumer resolves the WhoIs data via geo-client gRPC,
 * then updates the sign_in_event record with the whoIsId.
 *
 * Fail-open by design: if FindWhoIs fails, Geo is unavailable, or the update fails,
 * the message is ACKed. The ipAddress is already persisted for forensics.
 *
 * Direct exchange + named queue = competing consumers across Auth instances.
 */
export function createWhoIsResolutionConsumer(deps: WhoIsResolutionConsumerDeps) {
  const { messageBus, provider, createScope, findWhoIs, logger } = deps;

  return messageBus.subscribeEnriched<unknown>(
    {
      queue: AUTH_MESSAGING.WHOIS_RESOLUTION_QUEUE,
      queueOptions: { durable: true },
      prefetchCount: 1,
      exchanges: [
        {
          exchange: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE,
          type: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE_TYPE,
        },
      ],
      queueBindings: [
        {
          exchange: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE,
          routingKey: AUTH_MESSAGING.WHOIS_RESOLUTION_QUEUE,
        },
      ],
    },
    async (msg: IncomingMessage<unknown>) => {
      const parsed = validateMessage(msg.body);
      if (!parsed) {
        logger.warn("Invalid WhoIs resolution message — dropping", { body: msg.body });
        return ConsumerResult.ACK;
      }
      const { signInEventId, sessionId, ipAddress } = parsed;

      try {
        // Resolve WhoIs via geo-client (singleton, not scoped)
        const whoIsResult = await findWhoIs.handleAsync({ ipAddress });
        const whoIsId = whoIsResult.data?.whoIs?.hashId;

        if (!whoIsId) {
          // No WhoIs data available — fail-open, ipAddress is already persisted
          return ConsumerResult.ACK;
        }

        // Update the sign-in event record AND (if present) the session row with
        // the resolved whoIsId. Both are best-effort — failures here just mean
        // the row stays without a whoIsId; the FE falls back to the raw ipAddress.
        const scope = createScope(provider);
        try {
          const updateEvent = scope.resolve(IUpdateSignInEventWhoIsIdKey);
          await updateEvent.handleAsync({ id: signInEventId, whoIsId });

          if (sessionId) {
            const updateSession = scope.resolve(IUpdateSessionWhoIsIdKey);
            await updateSession.handleAsync({ id: sessionId, whoIsId });
          }
        } finally {
          scope.dispose();
        }
      } catch (error) {
        // Fail-open: log and ACK — ipAddress is already persisted for forensics
        logger.warn("WhoIs resolution failed for sign-in event — skipping", {
          signInEventId,
          sessionId,
          error: error instanceof Error ? error.message : String(error),
        });
      }

      return ConsumerResult.ACK;
    },
  );
}
