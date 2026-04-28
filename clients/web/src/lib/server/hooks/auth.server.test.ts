import { describe, it, expect, vi, beforeEach } from "vitest";
import type { RequestEvent } from "@sveltejs/kit";
import type { IRequestContext } from "@d2/handler";
import { OrgType } from "@d2/handler";
import { propagation, context, type Span, type Baggage } from "@opentelemetry/api";

const mockResolve = vi.fn();

vi.mock("../auth.server", () => ({
  getAuthContext: () => ({
    sessionResolver: { resolve: mockResolve },
  }),
}));

import { createAuthHandle } from "./auth.server";

// --- OTel spy helpers ---

const mockSetAttribute = vi.fn();
const mockSpan: Partial<Span> = { setAttribute: mockSetAttribute };

/** Captures baggage passed to propagation.createBaggage(). */
let capturedBaggageEntries: Record<string, { value: string }> = {};

/** Captures whether context.with() was called (baggage wrapping). */
let contextWithCalled = false;

beforeEach(() => {
  capturedBaggageEntries = {};
  contextWithCalled = false;
});

// Mock getServerSpan to return our mock span.
vi.mock("../../../instrumentation.server", () => ({
  getServerSpan: () => mockSpan,
}));

// Spy on propagation.createBaggage to capture entries.
vi.spyOn(propagation, "createBaggage").mockImplementation((entries) => {
  capturedBaggageEntries = (entries ?? {}) as Record<string, { value: string }>;
  return {} as Baggage;
});

// Spy on propagation.setBaggage to return the context as-is.
vi.spyOn(propagation, "setBaggage").mockImplementation((ctx) => ctx);

// Spy on context.active to return a dummy context.
const dummyContext = {} as ReturnType<typeof context.active>;
vi.spyOn(context, "active").mockReturnValue(dummyContext);

// Spy on context.with to execute the callback directly (no real OTel wrapping).
vi.spyOn(context, "with").mockImplementation((_ctx, fn) => {
  contextWithCalled = true;
  return (fn as () => unknown)();
});

describe("createAuthHandle", () => {
  const handle = createAuthHandle();

  function makeEvent(requestContext?: Partial<IRequestContext>): RequestEvent {
    return {
      request: new Request("http://localhost/test"),
      locals: {
        session: null,
        user: null,
        requestContext: requestContext
          ? ({
              clientIp: "127.0.0.1",
              serverFingerprint: "abc",
              deviceFingerprint: "abc123def456",
              isAuthenticated: false,
              isTrustedService: false,
              userId: undefined,
              isOrgEmulating: false,
              isUserImpersonating: false,
              isAgentStaff: false,
              isAgentAdmin: false,
              isTargetingStaff: false,
              isTargetingAdmin: false,
              ...requestContext,
            } as IRequestContext)
          : undefined,
      },
    } as unknown as RequestEvent;
  }

  const mockSvelteResolve = vi.fn().mockResolvedValue(new Response("OK"));

  beforeEach(() => {
    vi.clearAllMocks();
    // Re-apply OTel spies that clearAllMocks reset
    vi.spyOn(propagation, "createBaggage").mockImplementation((entries) => {
      capturedBaggageEntries = (entries ?? {}) as Record<string, { value: string }>;
      return {} as Baggage;
    });
    vi.spyOn(propagation, "setBaggage").mockImplementation((ctx) => ctx);
    vi.spyOn(context, "active").mockReturnValue(dummyContext);
    vi.spyOn(context, "with").mockImplementation((_ctx, fn) => {
      contextWithCalled = true;
      return (fn as () => unknown)();
    });
  });

  // --- Session resolution ---

  it("sets session and user on locals from resolver", async () => {
    const session = { userId: "user-1" };
    const user = { id: "user-1", email: "test@example.com" };
    mockResolve.mockResolvedValue({ session, user });

    const event = makeEvent();
    await handle({ event, resolve: mockSvelteResolve });

    expect(event.locals.session).toBe(session);
    expect(event.locals.user).toBe(user);
  });

  it("sets null session and user when resolver returns nulls", async () => {
    mockResolve.mockResolvedValue({ session: null, user: null });

    const event = makeEvent();
    await handle({ event, resolve: mockSvelteResolve });

    expect(event.locals.session).toBeNull();
    expect(event.locals.user).toBeNull();
  });

  // --- Request context population ---

  it("updates requestContext.isAuthenticated and userId when session exists", async () => {
    const session = { userId: "user-abc-123" };
    const user = { id: "user-abc-123" };
    mockResolve.mockResolvedValue({ session, user });

    const event = makeEvent({ isAuthenticated: false, userId: undefined });
    await handle({ event, resolve: mockSvelteResolve });

    expect(event.locals.requestContext!.isAuthenticated).toBe(true);
    expect(event.locals.requestContext!.userId).toBe("user-abc-123");
  });

  it("sets username on requestContext when user has username", async () => {
    const session = { userId: "user-1" };
    const user = { id: "user-1", username: "janedoe" };
    mockResolve.mockResolvedValue({ session, user });

    const event = makeEvent({ isAuthenticated: false });
    await handle({ event, resolve: mockSvelteResolve });

    expect(event.locals.requestContext!.username).toBe("janedoe");
  });

  it("does NOT set username when user has no username", async () => {
    const session = { userId: "user-1" };
    const user = { id: "user-1" };
    mockResolve.mockResolvedValue({ session, user });

    const event = makeEvent({ isAuthenticated: false, username: undefined });
    await handle({ event, resolve: mockSvelteResolve });

    expect(event.locals.requestContext!.username).toBeUndefined();
  });

  it("does NOT update requestContext when session is null", async () => {
    mockResolve.mockResolvedValue({ session: null, user: null });

    const event = makeEvent({ isAuthenticated: false, userId: undefined });
    await handle({ event, resolve: mockSvelteResolve });

    expect(event.locals.requestContext!.isAuthenticated).toBe(false);
    expect(event.locals.requestContext!.userId).toBeUndefined();
  });

  it("does NOT crash when requestContext is undefined", async () => {
    const session = { userId: "user-1" };
    mockResolve.mockResolvedValue({ session, user: { id: "user-1" } });

    const event = makeEvent(); // no requestContext
    await handle({ event, resolve: mockSvelteResolve });

    expect(event.locals.session).toBe(session);
    expect(event.locals.requestContext).toBeUndefined();
  });

  // --- Span enrichment ---

  it("enriches active span with auth + network fields for authenticated user", async () => {
    const session = { userId: "user-42" };
    const user = { id: "user-42", username: "johndoe" };
    mockResolve.mockResolvedValue({ session, user });

    const event = makeEvent({
      deviceFingerprint: "fp-abc",
      whoIsHashId: "abcdef1234567890",
      city: "Denver",
      countryCode: "US",
      isVpn: false,
      isProxy: false,
      isTor: false,
      isHosting: false,
    });
    await handle({ event, resolve: mockSvelteResolve });

    expect(mockSetAttribute).toHaveBeenCalledWith("userId", "user-42");
    expect(mockSetAttribute).toHaveBeenCalledWith("username", "johndoe");
    expect(mockSetAttribute).toHaveBeenCalledWith("deviceFingerprint", "fp-abc");
    expect(mockSetAttribute).toHaveBeenCalledWith("whoIsHashId", "abcdef1234567890");
    expect(mockSetAttribute).toHaveBeenCalledWith("city", "Denver");
    expect(mockSetAttribute).toHaveBeenCalledWith("countryCode", "US");
    expect(mockSetAttribute).toHaveBeenCalledWith("isAuthenticated", true);
    expect(mockSetAttribute).toHaveBeenCalledWith("isVpn", false);
    expect(mockSetAttribute).toHaveBeenCalledWith("isProxy", false);
    expect(mockSetAttribute).toHaveBeenCalledWith("isTor", false);
    expect(mockSetAttribute).toHaveBeenCalledWith("isHosting", false);
  });

  it("enriches active span with network fields only for anonymous user", async () => {
    mockResolve.mockResolvedValue({ session: null, user: null });

    const event = makeEvent({
      deviceFingerprint: "fp-xyz",
      whoIsHashId: "whois-hash-anon",
      city: "London",
      countryCode: "GB",
    });
    await handle({ event, resolve: mockSvelteResolve });

    expect(mockSetAttribute).toHaveBeenCalledWith("deviceFingerprint", "fp-xyz");
    expect(mockSetAttribute).toHaveBeenCalledWith("whoIsHashId", "whois-hash-anon");
    expect(mockSetAttribute).toHaveBeenCalledWith("city", "London");
    expect(mockSetAttribute).toHaveBeenCalledWith("countryCode", "GB");
    expect(mockSetAttribute).toHaveBeenCalledWith("isAuthenticated", false);
    // userId should NOT be set (it's undefined, enrichSpan skips nullish)
    expect(mockSetAttribute).not.toHaveBeenCalledWith("userId", expect.anything());
  });

  it("does not set span attributes for null/undefined values", async () => {
    mockResolve.mockResolvedValue({ session: null, user: null });

    // requestContext with most fields undefined
    const event = makeEvent({});
    await handle({ event, resolve: mockSvelteResolve });

    // isAuthenticated is always set (defaults to false)
    expect(mockSetAttribute).toHaveBeenCalledWith("isAuthenticated", false);
    // userId, username, city etc. should NOT be set
    const setAttrCalls = mockSetAttribute.mock.calls.map((call: unknown[]) => call[0] as string);
    expect(setAttrCalls).not.toContain("userId");
    expect(setAttrCalls).not.toContain("username");
    expect(setAttrCalls).not.toContain("city");
    expect(setAttrCalls).not.toContain("countryCode");
  });

  // --- Baggage propagation ---

  it("sets baggage with auth + network fields for authenticated user", async () => {
    const session = { userId: "user-99" };
    const user = { id: "user-99", username: "alice" };
    mockResolve.mockResolvedValue({ session, user });

    const event = makeEvent({
      deviceFingerprint: "fp-auth",
      whoIsHashId: "whois-hash-auth",
      city: "Paris",
      countryCode: "FR",
      agentOrgId: "org-1",
      agentOrgType: OrgType.Customer,
    });
    await handle({ event, resolve: mockSvelteResolve });

    expect(capturedBaggageEntries).toEqual({
      deviceFingerprint: { value: "fp-auth" },
      whoIsHashId: { value: "whois-hash-auth" },
      city: { value: "Paris" },
      countryCode: { value: "FR" },
      userId: { value: "user-99" },
      username: { value: "alice" },
      agentOrgId: { value: "org-1" },
      agentOrgType: { value: "customer" },
    });
  });

  it("sets baggage with network fields only for anonymous user", async () => {
    mockResolve.mockResolvedValue({ session: null, user: null });

    const event = makeEvent({
      deviceFingerprint: "fp-anon",
      whoIsHashId: "whois-hash-de",
      city: "Berlin",
      countryCode: "DE",
    });
    await handle({ event, resolve: mockSvelteResolve });

    // Only network fields — no userId, username, org fields
    expect(capturedBaggageEntries).toEqual({
      deviceFingerprint: { value: "fp-anon" },
      whoIsHashId: { value: "whois-hash-de" },
      city: { value: "Berlin" },
      countryCode: { value: "DE" },
    });
  });

  it("sets empty baggage when no requestContext (security: clears browser-injected baggage)", async () => {
    mockResolve.mockResolvedValue({ session: null, user: null });

    const event = makeEvent(); // no requestContext at all
    await handle({ event, resolve: mockSvelteResolve });

    // Empty baggage — overwrites any spoofed browser values
    expect(capturedBaggageEntries).toEqual({});
  });

  it("does not include username in baggage when user has no username", async () => {
    const session = { userId: "user-no-uname" };
    const user = { id: "user-no-uname" };
    mockResolve.mockResolvedValue({ session, user });

    const event = makeEvent({ deviceFingerprint: "fp-test" });
    await handle({ event, resolve: mockSvelteResolve });

    expect(capturedBaggageEntries.userId).toEqual({ value: "user-no-uname" });
    expect(capturedBaggageEntries.username).toBeUndefined();
  });

  it("does not include agentOrgId in baggage when not set", async () => {
    const session = { userId: "user-1" };
    const user = { id: "user-1" };
    mockResolve.mockResolvedValue({ session, user });

    const event = makeEvent({ agentOrgId: undefined });
    await handle({ event, resolve: mockSvelteResolve });

    expect(capturedBaggageEntries.agentOrgId).toBeUndefined();
    expect(capturedBaggageEntries.agentOrgType).toBeUndefined();
  });

  // --- Resolve wrapping ---

  it("always wraps resolve in OTel context.with (baggage propagation)", async () => {
    mockResolve.mockResolvedValue({ session: null, user: null });

    const event = makeEvent();
    await handle({ event, resolve: mockSvelteResolve });

    expect(contextWithCalled).toBe(true);
    expect(mockSvelteResolve).toHaveBeenCalledWith(event);
  });

  it("wraps resolve in baggage context for authenticated user", async () => {
    const session = { userId: "user-1" };
    mockResolve.mockResolvedValue({ session, user: { id: "user-1" } });

    const event = makeEvent();
    await handle({ event, resolve: mockSvelteResolve });

    expect(propagation.createBaggage).toHaveBeenCalled();
    expect(propagation.setBaggage).toHaveBeenCalled();
    expect(context.with).toHaveBeenCalled();
    expect(mockSvelteResolve).toHaveBeenCalledWith(event);
  });
});
