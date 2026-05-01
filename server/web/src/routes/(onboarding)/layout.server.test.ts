import { describe, it, expect } from "vitest";
import { load } from "./+layout.server";

describe("(onboarding) +layout.server.ts", () => {
  it("redirects to /sign-in when user is not authenticated", async () => {
    await expect(load({ locals: { session: null, user: null } } as any)).rejects.toMatchObject({
      status: 303,
      location: "/sign-in",
    });
  });

  it("allows access when user is authenticated", async () => {
    const result = await load({
      locals: {
        session: { userId: "user-1" },
        user: { id: "user-1", email: "a@b.com", name: "A" },
      },
    } as any);

    expect(result).toEqual({});
  });
});
