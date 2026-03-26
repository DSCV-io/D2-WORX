import { updateTimezone, bustSessionCache } from "$lib/client/rest/account-client.js";

export async function changeTimezone(timezone: string): Promise<void> {
  const result = await updateTimezone(timezone);
  if (!result.success) {
    throw new Error(result.messages?.[0] ?? "Failed to update timezone.");
  }
  await bustSessionCache();
}
