import type { User } from "@d2/auth-domain";

/**
 * Maps a BetterAuth user record to the domain User type.
 *
 * BetterAuth stores snake_case fields in the database (via casing config),
 * but surfaces camelCase in its API. This mapper handles both.
 */
export function toDomainUser(raw: Record<string, unknown>): User {
  return {
    id: raw["id"] as string,
    email: raw["email"] as string,
    name: raw["name"] as string,
    username: raw["username"] as string,
    displayUsername: (raw["displayUsername"] ?? raw["display_username"]) as string,
    emailVerified: Boolean(raw["emailVerified"] ?? raw["email_verified"]),
    image: (raw["image"] as string | undefined) ?? undefined,
    locale: (raw["locale"] as string) ?? "en-US",
    timezone: (raw["timezone"] as string) ?? "America/New_York",
    createdAt: toDate(raw["createdAt"] ?? raw["created_at"]),
    updatedAt: toDate(raw["updatedAt"] ?? raw["updated_at"]),
  };
}

function toDate(value: unknown): Date {
  if (value instanceof Date) return value;
  if (typeof value === "string" || typeof value === "number") {
    return new Date(value);
  }
  return new Date();
}
