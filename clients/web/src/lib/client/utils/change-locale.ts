/**
 * Shared locale change logic for both the language modal and profile dropdown.
 *
 * If authenticated, persists the locale to the user's account first,
 * then busts the BetterAuth cookie cache so the next page load reads
 * fresh session data, then updates Paraglide (which triggers a reload).
 *
 * If not authenticated, updates Paraglide immediately (cookie-only).
 */
import { setLocale, type Locale } from "$lib/paraglide/runtime";
import { updateLocale, bustSessionCache } from "$lib/client/rest/account-client.js";

export async function changeLocale(locale: string, isAuthenticated: boolean): Promise<void> {
  if (isAuthenticated) {
    const result = await updateLocale(locale);
    if (!result.success) {
      throw new Error(result.messages?.[0] ?? "Failed to update locale.");
    }
    await bustSessionCache();
  }

  setLocale(locale as Locale);
}
