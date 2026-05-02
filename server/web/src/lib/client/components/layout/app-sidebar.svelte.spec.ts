// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { page } from "vitest/browser";
import { describe, expect, it, beforeEach } from "vitest";
import { render } from "vitest-browser-svelte";
import TestWrapper from "./test-app-sidebar-wrapper.svelte";

describe("app-sidebar.svelte", () => {
  beforeEach(async () => {
    // Sidebar requires desktop viewport (>= 768px) to be visible
    await page.viewport(1024, 768);
  });

  it("should render the DCSV WORX logo text", async () => {
    render(TestWrapper);

    await expect.element(page.getByText("DCSV WORX")).toBeInTheDocument();
  });

  it("should show navigation items", async () => {
    render(TestWrapper);

    await expect.element(page.getByText("Dashboard")).toBeInTheDocument();
    await expect.element(page.getByText("Settings")).toBeInTheDocument();
    await expect.element(page.getByText("Profile")).toBeInTheDocument();
  });

  it("should render nav items as links with correct hrefs", async () => {
    render(TestWrapper);

    const dashboardLink = page.getByRole("link", { name: "Dashboard" });
    const settingsLink = page.getByRole("link", { name: "Settings" });
    const profileLink = page.getByRole("link", { name: "Profile" });

    await expect.element(dashboardLink).toHaveAttribute("href", "/dashboard");
    await expect.element(settingsLink).toHaveAttribute("href", "/settings");
    await expect.element(profileLink).toHaveAttribute("href", "/account/profile");
  });

  it("should display the org type from props", async () => {
    render(TestWrapper, { orgType: "support" });

    await expect.element(page.getByText("support")).toBeInTheDocument();
  });

  it("should show user placeholder in footer", async () => {
    render(TestWrapper);

    await expect.element(page.getByText("user@example.com")).toBeInTheDocument();
  });
});
