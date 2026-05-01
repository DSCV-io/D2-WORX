import { describe, it, expect, vi, beforeEach } from "vitest";
import type { LocaleOption } from "$lib/shared/forms/locale-options.js";

// Mock $env/dynamic/public — provide 2 locales for testing
vi.mock("$env/dynamic/public", () => ({
  env: {
    PUBLIC_ENABLED_LOCALES__0: "en-US",
    PUBLIC_ENABLED_LOCALES__1: "fr-FR",
  },
}));

// Mock Geo ref data — return consistent endonyms from "database"
vi.mock("$lib/server/geo-ref-data.server", () => ({
  getGeoRefData: vi.fn().mockResolvedValue({
    locales: {
      "en-US": {
        ietfBcp47Tag: "en-US",
        name: "English (United States)",
        endonym: "English (United States)",
        languageIso6391Code: "en",
        countryIso31661Alpha2Code: "US",
      },
      "fr-FR": {
        ietfBcp47Tag: "fr-FR",
        name: "French (France)",
        endonym: "Français (France)",
        languageIso6391Code: "fr",
        countryIso31661Alpha2Code: "FR",
      },
    },
  }),
}));

interface LayoutData {
  session: unknown;
  user: unknown;
  localeOptions: LocaleOption[];
}

const fakeUrl = { pathname: "/" } as URL;

// Stub `cookies` API — the layout loader reads D2_TIMEZONE; tests don't
// exercise that path so a no-op getter is enough to satisfy the call.
const fakeCookies = {
  get: () => undefined,
  getAll: () => [],
  set: () => {},
  delete: () => {},
  serialize: () => "",
};

async function callLoad(locals: Record<string, unknown> = {}): Promise<LayoutData> {
  // Dynamic import so mocks are in place before the module executes
  const { load } = await import("./+layout.server");
  return (await load({ locals, url: fakeUrl, cookies: fakeCookies } as any)) as LayoutData;
}

describe("root +layout.server.ts", () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it("should return null session and user when locals are empty", async () => {
    const result = await callLoad();

    expect(result.session).toBeNull();
    expect(result.user).toBeNull();
  });

  it("should pass through session from locals when present", async () => {
    const session = {
      userId: "user-123",
      activeOrganizationId: "org-456",
      activeOrganizationType: "customer",
      activeOrganizationRole: "owner",
    };

    const result = await callLoad({ session });

    expect(result.session).toEqual(session);
    expect(result.user).toBeNull();
  });

  it("should pass through user from locals when present", async () => {
    const user = {
      id: "user-123",
      email: "test@example.com",
      name: "Test User",
    };

    const result = await callLoad({ user });

    expect(result.session).toBeNull();
    expect(result.user).toEqual(user);
  });

  it("should include localeOptions from Geo ref data", async () => {
    const result = await callLoad();

    expect(result.localeOptions).toBeDefined();
    expect(result.localeOptions).toHaveLength(2);
    expect(result.localeOptions[0]).toEqual({
      code: "en-US",
      endonym: "English (United States)",
      flag: "/flags/4x3/us.svg",
    });
    expect(result.localeOptions[1]).toEqual({
      code: "fr-FR",
      endonym: "Français (France)",
      flag: "/flags/4x3/fr.svg",
    });
  });

  it("falls back to code-only options when Geo is unavailable", async () => {
    vi.doMock("$lib/server/geo-ref-data.server", () => ({
      getGeoRefData: vi.fn().mockResolvedValue(null),
    }));

    const result = await callLoad();

    expect(result.localeOptions).toHaveLength(2);
    expect(result.localeOptions[0]).toEqual({ code: "en-US", endonym: "en-US", flag: "" });
    expect(result.localeOptions[1]).toEqual({ code: "fr-FR", endonym: "fr-FR", flag: "" });
  });
});
