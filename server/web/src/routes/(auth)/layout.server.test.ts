import { describe, it, expect } from "vitest";
import { load } from "./+layout.server";

describe("(auth) +layout.server.ts", () => {
  it("allows access when user is not authenticated", async () => {
    const result = await load({
      locals: { session: null, user: null },
    } as any);

    expect(result).toEqual({});
  });

  it("redirects to /dashboard when user is already authenticated", async () => {
    await expect(
      load({
        locals: {
          session: { userId: "user-1", activeOrganizationId: "org-1" },
          user: { id: "user-1", email: "a@b.com", name: "A" },
        },
      } as any),
    ).rejects.toMatchObject({ status: 303, location: "/dashboard" });
  });
});
