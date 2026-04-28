import type { IHandler } from "@d2/handler";

export interface CheckPhoneAvailabilityInput {
  /** Digits-only E.164 (no `+`, 7-15 digits). */
  readonly phone: string;
  /** Optional: exclude this user from the lookup (useful when user is changing FROM their current phone). */
  readonly excludeUserId?: string;
}

export interface CheckPhoneAvailabilityOutput {
  readonly available: boolean;
}

export type ICheckPhoneAvailabilityHandler = IHandler<
  CheckPhoneAvailabilityInput,
  CheckPhoneAvailabilityOutput
>;
