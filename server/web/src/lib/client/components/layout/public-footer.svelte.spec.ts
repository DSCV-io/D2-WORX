import { page } from "vitest/browser";
import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-svelte";
import PublicFooter from "./public-footer.svelte";

describe("public-footer.svelte", () => {
  it("should render copyright text", async () => {
    render(PublicFooter);

    await expect.element(page.getByText(/DCSV\. All rights reserved/)).toBeInTheDocument();
  });

  it("should render Terms link", async () => {
    render(PublicFooter);

    const termsLink = page.getByRole("link", { name: "Terms" });
    await expect.element(termsLink).toBeInTheDocument();
    await expect.element(termsLink).toHaveAttribute("href", "/terms");
  });

  it("should render Privacy link", async () => {
    render(PublicFooter);

    const privacyLink = page.getByRole("link", { name: "Privacy" });
    await expect.element(privacyLink).toBeInTheDocument();
    await expect.element(privacyLink).toHaveAttribute("href", "/privacy");
  });
});
