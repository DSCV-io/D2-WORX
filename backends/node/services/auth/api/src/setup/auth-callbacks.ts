import type { ServiceProvider } from "@d2/di";
import { createServiceScope } from "@d2/handler";
import type { ILogger } from "@d2/logging";
import type { IMessagePublisher } from "@d2/messaging";
import type { GetContactsByExtKeys } from "@d2/geo-client";
import { INotifyKey } from "@d2/comms-client";
import { GEO_CONTEXT_KEYS, AUTH_MESSAGING } from "@d2/auth-domain";
import { IRecordSignInEventKey, ICreateUserContactKey } from "@d2/auth-app";
import type { AuthHooks } from "@d2/auth-infra";
import { signUpPrefsStorage } from "@d2/auth-infra";
import type { Translator } from "@d2/i18n";
import { resolveLocale } from "@d2/i18n";

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
      const scope = createCallbackScope();
      try {
        const contactId = await resolveUserContactId(
          getContactsByExtKeys,
          input.userId,
          logger,
          "password changed notification",
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
      } catch (err: unknown) {
        // Fail-open: password change is already committed, notification is best-effort.
        logger.warn("publishPasswordChanged: failed to send notification (fail-open)", {
          error: err instanceof Error ? err.message : String(err),
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
