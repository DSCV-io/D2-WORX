import { generateUuidV7 } from "@d2/utilities";
import { AuthValidationError } from "../exceptions/auth-validation-error.js";

export interface EmulationConsent {
  readonly id: string;
  readonly userId: string;
  readonly grantedToOrgId: string;
  readonly expiresAt: Date;
  readonly revokedAt?: Date;
  readonly createdAt: Date;
}

export interface CreateEmulationConsentInput {
  readonly userId: string;
  readonly grantedToOrgId: string;
  readonly expiresAt: Date;
  readonly id?: string;
}

export function createEmulationConsent(input: CreateEmulationConsentInput): EmulationConsent {
  if (!input.userId) {
    throw new AuthValidationError("EmulationConsent", "userId", input.userId, "is required.");
  }

  if (!input.grantedToOrgId) {
    throw new AuthValidationError(
      "EmulationConsent",
      "grantedToOrgId",
      input.grantedToOrgId,
      "is required.",
    );
  }

  if (!(input.expiresAt instanceof Date) || isNaN(input.expiresAt.getTime())) {
    throw new AuthValidationError(
      "EmulationConsent",
      "expiresAt",
      input.expiresAt,
      "must be a valid date.",
    );
  }

  if (input.expiresAt.getTime() <= Date.now()) {
    throw new AuthValidationError(
      "EmulationConsent",
      "expiresAt",
      input.expiresAt,
      "must be in the future.",
    );
  }

  return {
    id: input.id ?? generateUuidV7(),
    userId: input.userId,
    grantedToOrgId: input.grantedToOrgId,
    expiresAt: input.expiresAt,
    revokedAt: undefined,
    createdAt: new Date(),
  };
}

export function revokeEmulationConsent(consent: EmulationConsent): EmulationConsent {
  return {
    ...consent,
    revokedAt: new Date(),
  };
}

export function isConsentActive(consent: EmulationConsent): boolean {
  return consent.revokedAt == null && consent.expiresAt.getTime() > Date.now();
}
