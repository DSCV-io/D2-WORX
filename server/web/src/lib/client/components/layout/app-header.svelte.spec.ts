import { page } from "vitest/browser";
import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-svelte";
import TestWrapper from "./test-app-header-wrapper.svelte";

describe("app-header.svelte", () => {
  it("should render the sidebar trigger button", async () => {
    render(TestWrapper);

    const trigger = page.getByRole("button", { name: /toggle sidebar/i });
    await expect.element(trigger).toBeInTheDocument();
  });

  it("should render the preferences button", async () => {
    render(TestWrapper);

    const prefsButton = page.getByRole("button", { name: /preferences/i });
    await expect.element(prefsButton).toBeInTheDocument();
  });

  it("should render the breadcrumb area", async () => {
    render(TestWrapper);

    await expect.element(page.getByText("Dashboard")).toBeInTheDocument();
  });
});
