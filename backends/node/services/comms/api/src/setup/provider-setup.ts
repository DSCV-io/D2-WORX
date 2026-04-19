import type { ServiceCollection } from "@d2/di";
import type { IHandlerContext } from "@d2/handler";
import type { ILogger } from "@d2/logging";
import { IEmailProviderKey, ISmsProviderKey } from "@d2/comms-app";
import { MockSmsProvider, ResendEmailProvider, TwilioSmsProvider } from "@d2/comms-infra";

export type SmsProviderKind = "twilio" | "mock";

export interface ProviderConfig {
  resendApiKey?: string;
  resendFromAddress?: string;
  twilioAccountSid?: string;
  twilioAuthToken?: string;
  twilioPhoneNumber?: string;
  /**
   * Which SMS provider to wire. Explicit value wins; otherwise auto-detect:
   * if Twilio creds are present → "twilio", else → "mock".
   * The mock provider appends each message to a JSONL log file — useful for
   * dev where Twilio A2P 10DLC / Toll-Free verification gates real delivery.
   */
  smsProvider?: string;
  /** Path the mock provider writes to (JSONL). Default: /app/.dev-data/sms.jsonl */
  smsMockLogPath?: string;
}

const DEFAULT_MOCK_LOG_PATH = "/app/.dev-data/sms.jsonl";

function selectSmsProvider(config: ProviderConfig): SmsProviderKind {
  if (config.smsProvider === "twilio" || config.smsProvider === "mock") {
    return config.smsProvider;
  }
  const hasTwilio = !!(
    config.twilioAccountSid &&
    config.twilioAuthToken &&
    config.twilioPhoneNumber
  );
  return hasTwilio ? "twilio" : "mock";
}

/**
 * Registers email and SMS delivery providers as singleton instances.
 * Logs warnings when credentials are missing (graceful degradation).
 */
export function addDeliveryProviders(
  services: ServiceCollection,
  config: ProviderConfig,
  serviceContext: IHandlerContext,
  logger: ILogger,
): void {
  if (config.resendApiKey && config.resendFromAddress) {
    services.addInstance(
      IEmailProviderKey,
      new ResendEmailProvider(config.resendApiKey, config.resendFromAddress, serviceContext),
    );
  } else {
    logger.warn("No Resend API key configured — email delivery disabled");
  }

  const smsProvider = selectSmsProvider(config);
  if (smsProvider === "twilio") {
    if (config.twilioAccountSid && config.twilioAuthToken && config.twilioPhoneNumber) {
      services.addInstance(
        ISmsProviderKey,
        new TwilioSmsProvider(
          config.twilioAccountSid,
          config.twilioAuthToken,
          config.twilioPhoneNumber,
          serviceContext,
        ),
      );
      logger.info("SMS provider: Twilio");
    } else {
      logger.warn(
        "COMMS_SMS_PROVIDER=twilio but Twilio credentials are incomplete — SMS delivery disabled",
      );
    }
  } else {
    const logPath = config.smsMockLogPath ?? DEFAULT_MOCK_LOG_PATH;
    services.addInstance(ISmsProviderKey, new MockSmsProvider(logPath, serviceContext));
    logger.info("SMS provider: Mock (writes to JSONL log)", { logPath });
  }
}
