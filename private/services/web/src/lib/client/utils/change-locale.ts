// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Shared locale change logic for both the language modal and profile dropdown.
 *
 * Persists to backend if authenticated, updates Paraglide's cookie +
 * internal state, then forces a hard page reload so every `m.*()` call
 * re-evaluates with the new locale.
 *
 * Why we own the reload: Paraglide's `setLocale()` has a guard
 * `newLocale !== currentLocale` that silently skips the reload when the
 * cookie or global variable already holds the new value — which can
 * happen intermittently due to timing. Calling `reload: false` +
 * explicit `window.location.reload()` eliminates the race.
 */
import { setLocale, type Locale } from "$lib/paraglide/runtime";
import { updateLocale, bustSessionCache } from "$lib/client/rest/account-client.js";
import { translateMessage } from "$lib/client/utils/translate-message.js";
import * as m from "$lib/paraglide/messages.js";

export async function changeLocale(locale: string, isAuthenticated: boolean): Promise<void> {
  if (isAuthenticated) {
    const result = await updateLocale(locale);
    if (!result.success) {
      throw new Error(
        translateMessage(
          result.messages?.[0],
          undefined,
          m.webclient_app_account_profile_locale_update_failed(),
        ),
      );
    }
    await bustSessionCache();
  }

  // Update Paraglide's cookie + internal state WITHOUT its built-in reload.
  setLocale(locale as Locale, { reload: false });

  // Hard reload — Paraglide's m.*() calls are plain functions reading a
  // module-level variable, so only a full page reload picks up the change.
  window.location.reload();
}
