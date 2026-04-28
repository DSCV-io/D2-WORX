import { page } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-svelte";
import PublicNav from "./public-nav.svelte";

vi.mock("$app/stores", () => ({
  page: {
    subscribe: (fn: (value: unknown) => void) => {
      fn({ data: { session: null, user: null } });
      return () => {};
    },
  },
}));

vi.mock("$app/navigation", () => ({
  invalidateAll: () => Promise.resolve(),
  goto: () => Promise.resolve(),
}));

vi.mock("$app/paths", () => ({
  resolve: (path: string) => path,
}));

vi.mock("$lib/client/utils/avatar-url.js", () => ({
  getAvatarDisplayUrl: () => Promise.resolve(""),
  invalidateAvatarUrl: () => {},
  clearAvatarUrlCache: () => {},
}));

vi.mock("$lib/client/rest/files-client.js", () => ({
  getVariantUrl: () => Promise.resolve(""),
}));

vi.mock("$lib/client/stores/auth-client.js", () => ({
  authClient: { signOut: () => Promise.resolve() },
}));

vi.mock("$lib/client/rest/gateway-client.js", () => ({
  invalidateToken: () => {},
  getToken: () => Promise.resolve(null),
}));

vi.mock("$lib/paraglide/runtime", () => ({
  getLocale: () => "en",
  setLocale: () => {},
}));

vi.mock("$lib/paraglide/messages.js", () => ({
  webclient_nav_brand: () => "DCSV WORX",
  common_ui_sign_in: () => "Sign In",
  common_ui_sign_up: () => "Sign Up",
  common_ui_sign_out: () => "Sign Out",
  common_ui_dashboard: () => "Dashboard",
  common_ui_preferences: () => "Preferences",
  common_ui_open_menu: () => "Open menu",
  common_ui_mode: () => "Mode",
  common_ui_mode_light: () => "Light",
  common_ui_mode_system: () => "System",
  common_ui_mode_dark: () => "Dark",
  common_ui_theme: () => "Theme",
  common_ui_language: () => "Language",
  common_ui_account: () => "Account",
  common_ui_profile: () => "Profile",
  common_ui_toggle_mode: () => "Toggle mode",
  common_ui_select_theme: () => "Select theme",
  common_ui_choose_language: () => "Choose your preferred language.",
  common_ui_set_language: () => "Set Language",
  common_ui_cancel: () => "Cancel",
}));

describe("public-nav.svelte", () => {
  it("should render the brand text on desktop", async () => {
    render(PublicNav);

    await expect.element(page.getByText("DCSV WORX")).toBeInTheDocument();
  });

  it("should render the Sign In link pointing to /sign-in", async () => {
    render(PublicNav);

    const signInLink = page.getByRole("link", { name: /sign in/i });
    await expect.element(signInLink).toBeInTheDocument();
    await expect.element(signInLink).toHaveAttribute("href", "/sign-in");
  });

  it("should render the preferences dropdown button when logged out", async () => {
    render(PublicNav);

    const prefsButton = page.getByRole("button", { name: /preferences/i });
    await expect.element(prefsButton).toBeInTheDocument();
  });

  it("should render the mobile hamburger button when logged out", async () => {
    render(PublicNav);

    const menuButton = page.getByRole("button", { name: /open menu/i });
    await expect.element(menuButton).toBeInTheDocument();
  });
});
