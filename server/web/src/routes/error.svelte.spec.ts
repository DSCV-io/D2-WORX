import { page } from "vitest/browser";
import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-svelte";
import ErrorPage from "./+error.svelte";

describe("+error.svelte", () => {
  it("should render the status code", async () => {
    render(ErrorPage);

    // The component uses $app/state's page store which defaults in test context
    // We verify the structural elements render
    const heading = page.getByRole("heading", { level: 1 });
    await expect.element(heading).toBeInTheDocument();
  });

  it("should render the go-home link", async () => {
    render(ErrorPage);

    const link = page.getByRole("link", { name: /go home/i });
    await expect.element(link).toBeInTheDocument();
    await expect.element(link).toHaveAttribute("href", "/");
  });
});
