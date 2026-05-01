import { describe, it, expect } from "vitest";
import { load } from "./+layout.server";

describe("(app) +layout.server.ts", () => {
  it("redirects to /sign-in when session is null", async () => {
    await expect(load({ locals: { session: null, user: null } } as any)).rejects.toMatchObject({
      status: 303,
      location: "/sign-in",
    });
  });

  it("redirects to /welcome when session has no active org", async () => {
    await expect(
      load({
        locals: {
          session: {
            userId: "user-123",
            activeOrganizationId: null,
            activeOrganizationType: null,
            activeOrganizationRole: null,
          },
          user: { id: "user-123", email: "a@b.com", name: "A" },
        },
      } as any),
    ).rejects.toMatchObject({ status: 303, location: "/welcome" });
  });

  it("returns orgType and role from session with active org", async () => {
    const result = await load({
      locals: {
        session: {
          userId: "user-123",
          activeOrganizationId: "org-1",
          activeOrganizationType: "support",
          activeOrganizationRole: "officer",
        },
        user: { id: "user-123", email: "a@b.com", name: "A" },
      },
    } as any);

    expect(result).toEqual({
      orgType: "support",
      role: "officer",
    });
  });
});
