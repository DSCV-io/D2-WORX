// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Client-side utility for calling account endpoints.
 *
 * Account endpoints return D2Result responses and go through
 * SvelteKit's /api/account/* proxy to the Auth service.
 * Same pattern as auth-gateway-client.ts.
 */
import { D2Result } from "@dcsv-io/d2-result";
import { executeFetch } from "$lib/shared/rest/gateway-response.js";

const DEFAULT_TIMEOUT_MS = 10_000;

interface AccountApiCallOptions {
  method?: "GET" | "POST" | "PATCH" | "DELETE";
  body?: unknown;
  signal?: AbortSignal;
  timeout?: number;
}

async function accountApiCall<TData = void>(
  path: string,
  options?: AccountApiCallOptions,
): Promise<D2Result<TData>> {
  const headers = new Headers();
  if (options?.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }
  // CSRF: non-simple Content-Type ensures Origin header is sent
  if (!headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  return executeFetch<TData>(path, {
    method: options?.method,
    headers,
    body: options?.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: options?.signal,
    timeout: options?.timeout ?? DEFAULT_TIMEOUT_MS,
    credentials: "include",
  });
}

/** Update user's real name (first + last). */
export async function updateName(firstName: string, lastName: string): Promise<D2Result> {
  return accountApiCall("/api/account/name", {
    method: "PATCH",
    body: { firstName, lastName },
  });
}

/** Update username. */
export async function updateUsername(username: string): Promise<D2Result> {
  return accountApiCall("/api/account/username", {
    method: "PATCH",
    body: { username },
  });
}

/** Remove the user's profile picture. */
export async function removeAvatar(): Promise<D2Result> {
  return accountApiCall("/api/account/avatar", {
    method: "DELETE",
  });
}

/** Update user's locale preference. */
export async function updateLocale(locale: string): Promise<D2Result> {
  return accountApiCall("/api/account/locale", {
    method: "PATCH",
    body: { locale },
  });
}

/** Update user's timezone preference. */
export async function updateTimezone(timezone: string): Promise<D2Result> {
  return accountApiCall("/api/account/timezone", {
    method: "PATCH",
    body: { timezone },
  });
}

/**
 * Bust BetterAuth's cookie cache so the next page load reads fresh session data.
 * Call after mutations that change user fields (locale, etc.) before triggering
 * a page reload.
 */
export async function bustSessionCache(): Promise<void> {
  await fetch("/api/auth/get-session?disableCookieCache=true", {
    method: "GET",
    credentials: "include",
  }).catch(() => {});
}

// ---------------------------------------------------------------------------
// Email & Phone change (OTP-gated, password-confirmed)
// ---------------------------------------------------------------------------

/**
 * Initiate an email change. Server validates currentPassword atomically with
 * newEmail (no bypass). On success, server sends a 6-digit OTP to newEmail
 * and returns the expiry timestamp for the frontend countdown.
 */
export async function requestEmailChange(
  newEmail: string,
  currentPassword: string,
): Promise<D2Result<{ expiresAt: string }>> {
  return accountApiCall<{ expiresAt: string }>("/api/account/email/request-change", {
    method: "POST",
    body: { newEmail, currentPassword },
  });
}

/** Submit the OTP code from the user's new email inbox to finalize the change. */
export async function verifyEmailChange(code: string): Promise<D2Result<{ newEmail: string }>> {
  return accountApiCall<{ newEmail: string }>("/api/account/email/verify-change", {
    method: "POST",
    body: { code },
  });
}

/**
 * Initiate a phone add or change. newPhone MUST be digits-only (frontend
 * strips formatting via `phoneToDigits()` before calling). Server validates
 * password + sends OTP via SMS to the new phone.
 */
export async function requestPhoneChange(
  newPhone: string,
  currentPassword: string,
): Promise<D2Result<{ expiresAt: string }>> {
  return accountApiCall<{ expiresAt: string }>("/api/account/phone/request-change", {
    method: "POST",
    body: { newPhone, currentPassword },
  });
}

/** Submit the OTP code from the user's phone to finalize the phone change. */
export async function verifyPhoneChange(code: string): Promise<D2Result<{ phone: string }>> {
  return accountApiCall<{ phone: string }>("/api/account/phone/verify-change", {
    method: "POST",
    body: { code },
  });
}

/** Remove the user's phone number. Password gate is the sole defense. */
export async function removePhone(currentPassword: string): Promise<D2Result> {
  return accountApiCall("/api/account/phone", {
    method: "DELETE",
    body: { currentPassword },
  });
}

// ---------------------------------------------------------------------------
// Security tab — sessions, sign-in events, change password
// ---------------------------------------------------------------------------

/** Active session enriched with WhoIs + isCurrent flag (raw shape from API). */
export interface ActiveSessionDTO {
  session: {
    id: string;
    token: string;
    userId: string;
    expiresAt: string;
    createdAt: string;
    updatedAt: string;
    ipAddress?: string;
    userAgent?: string;
    whoIsId?: string;
    /** Combined sha256(clientFp + serverFp + clientIp). */
    deviceFingerprint?: string;
    /** Stable hardware/browser signature — used by identicons. */
    clientFingerprint?: string;
    /** Network-derived signature (UA + headers + IP class). */
    serverFingerprint?: string;
  };
  whoIs?: WhoIsLite;
  isCurrent: boolean;
}

/** Sign-in event enriched with WhoIs (raw shape from API). */
export interface RecentLoginDTO {
  event: {
    id: string;
    userId: string;
    successful: boolean;
    ipAddress: string;
    userAgent: string;
    whoIsId?: string;
    deviceFingerprint?: string;
    /** Stable hardware/browser signature — used by identicons. */
    clientFingerprint?: string;
    /** Network-derived signature (UA + headers + IP class). */
    serverFingerprint?: string;
    failureReason?: string;
    createdAt: string;
  };
  whoIs?: WhoIsLite;
}

/**
 * Subset of `WhoIsDTO` the FE actually renders. Mirror of the proto shape but
 * trimmed — JSON serialization drops the proto type info anyway.
 */
export interface WhoIsLite {
  hashId: string;
  ipAddress: string;
  asn?: number;
  asName?: string;
  asDomain?: string;
  isAnonymous?: boolean;
  isHosting?: boolean;
  isMobile?: boolean;
  isProxy?: boolean;
  isRelay?: boolean;
  isTor?: boolean;
  isVpn?: boolean;
  location?: {
    city?: string;
    postalCode?: string;
    subdivisionIso31662Code?: string;
    countryIso31661Alpha2Code?: string;
  };
}

export async function listMySessions(): Promise<D2Result<{ sessions: ActiveSessionDTO[] }>> {
  return accountApiCall<{ sessions: ActiveSessionDTO[] }>("/api/account/sessions");
}

export async function revokeSession(token: string, currentPassword: string): Promise<D2Result> {
  return accountApiCall("/api/account/sessions/revoke", {
    method: "POST",
    body: { token, currentPassword },
  });
}

export async function revokeOtherSessions(currentPassword: string): Promise<D2Result> {
  return accountApiCall("/api/account/sessions/revoke-others", {
    method: "POST",
    body: { currentPassword },
  });
}

export async function listRecentLogins(
  limit = 20,
  offset = 0,
): Promise<D2Result<{ events: RecentLoginDTO[]; total: number }>> {
  const qs = new URLSearchParams({ limit: String(limit), offset: String(offset) }).toString();
  return accountApiCall<{ events: RecentLoginDTO[]; total: number }>(
    `/api/account/sign-in-events?${qs}`,
  );
}

/**
 * Change password. By default revokes ALL other sessions to invalidate any
 * stolen cookies; opt out with `revokeOtherSessions=false`.
 */
export async function changePassword(
  currentPassword: string,
  newPassword: string,
  revokeOtherSessions = true,
): Promise<D2Result> {
  return accountApiCall("/api/account/change-password", {
    method: "POST",
    body: { currentPassword, newPassword, revokeOtherSessions },
  });
}

// ---------------------------------------------------------------------------
// Account deletion (self-service, atomic password gate, 30-day grace)
// ---------------------------------------------------------------------------

/**
 * Initiate self-service account deletion. Atomic password gate — wrong password
 * returns 401. Sole-owner-of-org returns 409. On success, server flips status
 * to `pending_deletion`, revokes all sessions, and returns the date when
 * permanent anonymization will run if the user does not sign back in.
 */
export async function requestUserDeletion(
  currentPassword: string,
  feedback?: { reason?: string; comment?: string },
): Promise<D2Result<{ scheduledFor: string }>> {
  return accountApiCall<{ scheduledFor: string }>("/api/account/delete", {
    method: "POST",
    body: { currentPassword, feedback },
  });
}
