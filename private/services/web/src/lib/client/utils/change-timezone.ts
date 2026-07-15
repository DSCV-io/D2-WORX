// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Shared timezone change logic for both the timezone modal and profile page.
 *
 * Sets the D2_TIMEZONE cookie (readable by the server for SSR / sign-up),
 * persists to backend if authenticated, then invalidates all server loads
 * so components re-render with the new timezone.
 *
 * Unlike locale, timezone does NOT require a page reload — no translations
 * depend on it, so invalidateAll() is sufficient.
 */
import { invalidateAll } from "$app/navigation";
import { updateTimezone, bustSessionCache } from "$lib/client/rest/account-client.js";
import { translateMessage } from "$lib/client/utils/translate-message.js";
import * as m from "$lib/paraglide/messages.js";

/** Cookie name — matches the server-side sync in +layout.server.ts. */
const TIMEZONE_COOKIE = "D2_TIMEZONE";
const COOKIE_MAX_AGE = 34_560_000; // ~400 days, same as PARAGLIDE_LOCALE

export async function changeTimezone(timezone: string, isAuthenticated: boolean): Promise<void> {
  if (isAuthenticated) {
    const result = await updateTimezone(timezone);
    if (!result.success) {
      throw new Error(
        translateMessage(
          result.messages?.[0],
          undefined,
          m.webclient_app_account_profile_timezone_update_failed(),
        ),
      );
    }
    await bustSessionCache();
  }

  // Set cookie so the server knows the timezone (SSR, sign-up, etc.)
  // No encodeURIComponent — IANA identifiers (A-Za-z0-9/_+-) are cookie-safe.
  document.cookie = `${TIMEZONE_COOKIE}=${timezone}; path=/; max-age=${COOKIE_MAX_AGE}; SameSite=Lax`;

  // Re-run all server loads so components pick up the new timezone.
  await invalidateAll();
}
