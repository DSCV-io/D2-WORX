import type { ServiceProvider } from "@d2/di";
import { createServiceScope } from "@d2/handler";
import type { ILogger } from "@d2/logging";
import type { IMessagePublisher } from "@d2/messaging";
import type { GetContactsByExtKeys } from "@d2/geo-client";
import { INotifyKey } from "@d2/comms-client";
import { GEO_CONTEXT_KEYS, AUTH_MESSAGING } from "@d2/auth-domain";
import {
  IRecordSignInEventKey,
  ICreateUserContactKey,
  IFindUserIdByIdentifierKey,
  ICancelUserDeletionKey,
} from "@d2/auth-app";
import type { AuthHooks } from "@d2/auth-infra";
import { signUpPrefsStorage } from "@d2/auth-infra";
import type { Translator } from "@d2/i18n";
import { resolveLocale } from "@d2/i18n";
import type { RecordFailedSignIn } from "../routes/auth-routes.js";

/**
 * Creates the BetterAuth callback hooks that bridge app-layer logic
 * into the auth-infra createAuth() call.
 */
export function createAuthCallbacks(
  provider: ServiceProvider,
  logger: ILogger,
  getContactsByExtKeys: GetContactsByExtKeys,
  translator: Translator,
  publisher?: IMessagePublisher,
): AuthHooks {
  const createCallbackScope = () => createServiceScope(provider, logger);

  return {
    onSignIn: async (data) => {
      const scope = createCallbackScope();
      try {
        const handler = scope.resolve(IRecordSignInEventKey);
        const result = await handler.handleAsync({
          userId: data.userId,
          successful: true,
          ipAddress: data.ipAddress,
          userAgent: data.userAgent,
          deviceFingerprint: data.deviceFingerprint,
        });

        // Fire-and-forget: publish to WhoIs resolution queue for async enrichment
        // of BOTH the sign_in_event row and the session row (consumer updates both).
        if (result.success && result.data?.event && publisher) {
          publisher
            .send(
              {
                exchange: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE,
                routingKey: AUTH_MESSAGING.WHOIS_RESOLUTION_QUEUE,
              },
              {
                signInEventId: result.data.event.id,
                sessionId: data.sessionId,
                ipAddress: data.ipAddress,
                userAgent: data.userAgent,
              },
            )
            .catch((err: unknown) =>
              logger.warn("onSignIn: WhoIs resolution publish failed (fail-open)", {
                error: err instanceof Error ? err.message : String(err),
              }),
            ); // Fail-open: ipAddress already persisted
        }
      } finally {
        scope.dispose();
      }
    },

    publishVerificationEmail: async (input) => {
      const scope = createCallbackScope();
      try {
        const contactId = await resolveUserContactId(
          getContactsByExtKeys,
          input.userId,
          logger,
          "verification email",
        );
        if (!contactId) return;

        const t = translator.t;
        const locale = resolveLocale(
          ((input as Record<string, unknown>).locale as string | undefined) ??
            signUpPrefsStorage.getStore()?.locale,
        );

        const notifier = scope.resolve(INotifyKey);
        await notifier.handleAsync({
          recipientContactId: contactId,
          title: t(locale, "auth_email_verification_subject"),
          content: [
            t(locale, "auth_email_verification_greeting", { name: input.name }),
            "",
            t(locale, "auth_email_verification_body"),
            "",
            `[${t(locale, "auth_email_verification_action")}](${input.verificationUrl})`,
            "",
            t(locale, "auth_email_link_fallback"),
            input.verificationUrl,
          ].join("\n"),
          plaintext: t(locale, "auth_email_verification_plaintext", {
            name: input.name,
            url: input.verificationUrl,
          }),
          channels: ["email"],
          correlationId: crypto.randomUUID(),
          senderService: "auth",
        });
      } finally {
        scope.dispose();
      }
    },

    publishPasswordReset: async (input) => {
      const scope = createCallbackScope();
      try {
        const contactId = await resolveUserContactId(
          getContactsByExtKeys,
          input.userId,
          logger,
          "password reset",
        );
        if (!contactId) return;

        const t = translator.t;
        const locale = resolveLocale(
          ((input as Record<string, unknown>).locale as string | undefined) ??
            signUpPrefsStorage.getStore()?.locale,
        );

        const notifier = scope.resolve(INotifyKey);
        await notifier.handleAsync({
          recipientContactId: contactId,
          title: t(locale, "auth_email_password_reset_subject"),
          content: [
            t(locale, "auth_email_password_reset_greeting", { name: input.name }),
            "",
            t(locale, "auth_email_password_reset_body"),
            "",
            `[${t(locale, "auth_email_password_reset_action")}](${input.resetUrl})`,
            "",
            t(locale, "auth_email_link_fallback"),
            input.resetUrl,
            "",
            t(locale, "auth_email_password_reset_disclaimer"),
          ].join("\n"),
          plaintext: t(locale, "auth_email_password_reset_plaintext", {
            name: input.name,
            url: input.resetUrl,
          }),
          channels: ["email"],
          correlationId: crypto.randomUUID(),
          senderService: "auth",
        });
      } finally {
        scope.dispose();
      }
    },

    publishPasswordChanged: async (input) => {
      logger.info("publishPasswordChanged: invoked", { userId: input.userId });
      const scope = createCallbackScope();
      try {
        const contactId = await resolveUserContactId(
          getContactsByExtKeys,
          input.userId,
          logger,
          "password changed notification",
        );
        if (!contactId) {
          logger.warn("publishPasswordChanged: no Geo contact for user — security email skipped", {
            userId: input.userId,
          });
          return;
        }

        const t = translator.t;
        const locale = resolveLocale(
          ((input as Record<string, unknown>).locale as string | undefined) ??
            signUpPrefsStorage.getStore()?.locale,
        );

        const notifier = scope.resolve(INotifyKey);
        const result = await notifier.handleAsync({
          recipientContactId: contactId,
          title: t(locale, "auth_email_password_changed_subject"),
          content: [
            t(locale, "auth_email_password_changed_greeting", { name: input.name }),
            "",
            t(locale, "auth_email_password_changed_body"),
            "",
            t(locale, "auth_email_password_changed_disclaimer"),
          ].join("\n"),
          plaintext: t(locale, "auth_email_password_changed_plaintext", { name: input.name }),
          channels: ["email"],
          correlationId: crypto.randomUUID(),
          senderService: "auth",
        });
        if (!result.success) {
          logger.warn("publishPasswordChanged: Notify returned non-success", {
            userId: input.userId,
            statusCode: result.statusCode,
            errorCode: result.errorCode,
            messages: result.messages,
          });
        } else {
          logger.info("publishPasswordChanged: security email queued", { userId: input.userId });
        }
      } catch (err: unknown) {
        // Fail-open: password change is already committed, notification is best-effort.
        logger.warn("publishPasswordChanged: failed to send notification (fail-open)", {
          error: err instanceof Error ? err.message : String(err),
          userId: input.userId,
        });
      } finally {
        scope.dispose();
      }
    },

    createUserContact: async (data) => {
      const scope = createCallbackScope();
      try {
        const handler = scope.resolve(ICreateUserContactKey);
        const result = await handler.handleAsync(data);
        if (!result.success) {
          throw new Error(
            `Failed to create Geo contact for user ${data.userId}: ${result.messages?.join(", ") ?? "unknown error"}`,
          );
        }
      } finally {
        scope.dispose();
      }
    },

    cancelUserDeletion: async (data) => {
      // Wrapper around the CancelUserDeletion app handler — invoked from the
      // BetterAuth sign-in `before` hook. Per-call DI scope keeps the traceId
      // isolated from any concurrent callbacks.
      const scope = createCallbackScope();
      try {
        const handler = scope.resolve(ICancelUserDeletionKey);
        const result = await handler.handleAsync({ userId: data.userId });
        if (!result.success) {
          logger.warn("cancelUserDeletion: handler returned non-success (non-blocking)", {
            userId: data.userId,
            statusCode: result.statusCode,
            errorCode: result.errorCode,
            messages: result.messages,
          });
        }
      } finally {
        scope.dispose();
      }
    },
  };
}

/**
 * Builds the audit-record callback for FAILED sign-in attempts.
 *
 * Resolves the userId from the supplied email/username — if no user matches
 * (attacker probing nonexistent identifiers) the failure is dropped from the
 * audit table (the throttle layer still tracks it by hashed identifier).
 *
 * On a successful resolution, writes a `sign_in_event` row with `successful:
 * false` + the failure reason, then publishes a WhoIs resolution message so
 * the row gets enriched with city/country/ASN — same pipeline as the success
 * path (`onSignIn`).
 */
export function createRecordFailedSignIn(
  provider: ServiceProvider,
  logger: ILogger,
  publisher?: IMessagePublisher,
): RecordFailedSignIn {
  return async (data) => {
    const scope = createServiceScope(provider, logger);
    try {
      // Resolve userId — drop the audit if nobody matches.
      const finder = scope.resolve(IFindUserIdByIdentifierKey);
      const lookup = await finder.handleAsync({
        email: data.email?.toLowerCase(),
        username: data.username?.toLowerCase(),
      });
      const userId = lookup.success ? lookup.data?.userId : undefined;
      if (!userId) return;

      const recorder = scope.resolve(IRecordSignInEventKey);
      const result = await recorder.handleAsync({
        userId,
        successful: false,
        ipAddress: data.ipAddress,
        userAgent: data.userAgent,
        deviceFingerprint: data.deviceFingerprint,
        failureReason: data.failureReason,
      });

      // Enqueue WhoIs resolution (no sessionId — failed attempts have no session).
      if (result.success && result.data?.event && publisher) {
        publisher
          .send(
            {
              exchange: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE,
              routingKey: AUTH_MESSAGING.WHOIS_RESOLUTION_QUEUE,
            },
            {
              signInEventId: result.data.event.id,
              ipAddress: data.ipAddress,
              userAgent: data.userAgent,
            },
          )
          .catch((err: unknown) =>
            logger.warn("recordFailedSignIn: WhoIs resolution publish failed (fail-open)", {
              error: err instanceof Error ? err.message : String(err),
            }),
          );
      }
    } finally {
      scope.dispose();
    }
  };
}

/**
 * Resolves a user's Geo contactId via ext-key lookup.
 * Returns undefined (with a log warning) if no contact is found.
 */
async function resolveUserContactId(
  getContactsByExtKeys: GetContactsByExtKeys,
  userId: string,
  logger: ILogger,
  purpose: string,
): Promise<string | undefined> {
  const result = await getContactsByExtKeys.handleAsync({
    keys: [{ contextKey: GEO_CONTEXT_KEYS.USER, relatedEntityId: userId }],
  });
  const lookupKey = `${GEO_CONTEXT_KEYS.USER}:${userId}`;
  const contactId = result.data?.data.get(lookupKey)?.[0]?.id;
  if (!contactId) {
    logger.error(`No Geo contact found for user ${userId} — cannot send ${purpose}`);
  }
  return contactId;
}
