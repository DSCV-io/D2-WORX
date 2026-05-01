import { page } from "vitest/browser";
import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-svelte";
import ThemeToggle from "./theme-toggle.svelte";

describe("theme-toggle.svelte", () => {
  it("should render a button with accessible label", async () => {
    render(ThemeToggle);

    const button = page.getByRole("button", { name: /toggle mode/i });
    await expect.element(button).toBeInTheDocument();
  });

  it("should contain an SVG icon", async () => {
    render(ThemeToggle);

    const button = page.getByRole("button", { name: /toggle mode/i });
    await expect.element(button).toBeInTheDocument();
    const svg = button.element().querySelector("svg");
    expect(svg).not.toBeNull();
  });

  it("should open a dropdown with Light, Dark, and System options", async () => {
    render(ThemeToggle);

    const button = page.getByRole("button", { name: /toggle mode/i });
    await button.click();

    await expect.element(page.getByRole("menuitem", { name: /light/i })).toBeInTheDocument();
    await expect.element(page.getByRole("menuitem", { name: /dark/i })).toBeInTheDocument();
    await expect.element(page.getByRole("menuitem", { name: /system/i })).toBeInTheDocument();
  });
});
