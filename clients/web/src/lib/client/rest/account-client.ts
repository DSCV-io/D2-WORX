/**
 * Client-side utility for calling account endpoints.
 *
 * Account endpoints return D2Result responses and go through
 * SvelteKit's /api/account/* proxy to the Auth service.
 * Same pattern as auth-gateway-client.ts.
 */
import { D2Result } from "@d2/result";
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
