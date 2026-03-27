/**
 * Shared locale change logic for both the language modal and profile dropdown.
 *
 * Updates Paraglide's internal locale (no reload), sets the cookie,
 * persists to backend if authenticated, then invalidates all server
 * loads so components re-render with the new translations.
 */
import { invalidateAll } from "$app/navigation";
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

  // Update Paraglide's internal locale + cookie WITHOUT triggering page reload.
  setLocale(locale as Locale);

  // Re-run all server loads — components re-render with the new locale.
  await invalidateAll();
}
