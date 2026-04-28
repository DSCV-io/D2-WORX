import { describe, it, expect, beforeEach, vi } from "vitest";
import { D2Result } from "@d2/result";
import { GetUserChannelPreference } from "@d2/comms-app";
import type { ChannelPreference } from "@d2/comms-domain";
import type { IGetContactsByExtKeysHandler, GetContactsByExtKeysOutput } from "@d2/geo-client";
import type { ContactDTO } from "@d2/protos";
import type { Queries } from "@d2/comms-app";
import { createMockContext } from "../helpers/mock-handlers.js";

const CONTEXT_KEY = "auth_user";
const RELATED_ENTITY_ID = "user-123";
const CONTACT_ID = "019505a0-1234-7abc-8000-000000000002";
const MAP_KEY = `${CONTEXT_KEY}:${RELATED_ENTITY_ID}`;

function buildPref(overrides?: Partial<ChannelPreference>): ChannelPreference {
  const now = new Date();
  return {
    id: "pref-id",
    contactId: CONTACT_ID,
    emailEnabled: true,
    smsEnabled: false,
    createdAt: now,
    updatedAt: now,
    ...overrides,
  };
}

function buildContactsMap(contacts: ContactDTO[] = []): GetContactsByExtKeysOutput {
  const map = new Map<string, ContactDTO[]>();
  if (contacts.length > 0) map.set(MAP_KEY, contacts);
  return { data: map };
}

function fakeContact(id = CONTACT_ID): ContactDTO {
  return { id, contextKey: CONTEXT_KEY, relatedEntityId: RELATED_ENTITY_ID } as ContactDTO;
}

function createMockGetContactsByExtKeys(): IGetContactsByExtKeysHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: buildContactsMap() })),
    redaction: { suppressOutput: true },
  } as unknown as IGetContactsByExtKeysHandler;
}

function createMockInner(): Queries.IGetChannelPreferenceHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.notFound()),
  } as unknown as Queries.IGetChannelPreferenceHandler;
}

describe("GetUserChannelPreference", () => {
  let geo: IGetContactsByExtKeysHandler;
  let inner: Queries.IGetChannelPreferenceHandler;
  let handler: GetUserChannelPreference;

  beforeEach(() => {
    geo = createMockGetContactsByExtKeys();
    inner = createMockInner();
    handler = new GetUserChannelPreference(geo, inner, createMockContext());
  });

  // ---------------------------------------------------------------------------
  // Validation
  // ---------------------------------------------------------------------------

  it("rejects empty contextKey", async () => {
    const result = await handler.handleAsync({
      contextKey: "",
      relatedEntityId: RELATED_ENTITY_ID,
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
    expect(geo.handleAsync).not.toHaveBeenCalled();
    expect(inner.handleAsync).not.toHaveBeenCalled();
  });

  it("rejects empty relatedEntityId", async () => {
    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: "",
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
    expect(geo.handleAsync).not.toHaveBeenCalled();
  });

  it("rejects contextKey exceeding 64 chars", async () => {
    const result = await handler.handleAsync({
      contextKey: "a".repeat(65),
      relatedEntityId: RELATED_ENTITY_ID,
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("rejects relatedEntityId exceeding 64 chars", async () => {
    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: "a".repeat(65),
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  // ---------------------------------------------------------------------------
  // Geo lookup failures → SERVICE_UNAVAILABLE (no defaults leak)
  // ---------------------------------------------------------------------------

  it("returns 503 when Geo lookup fails", async () => {
    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.fail({ messages: ["geo down"], statusCode: 500 }),
    );

    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
    expect(inner.handleAsync).not.toHaveBeenCalled();
  });

  // ---------------------------------------------------------------------------
  // No contact yet → ok({ pref: undefined }) — caller treats as "use defaults"
  // ---------------------------------------------------------------------------

  it("returns ok({ pref: undefined }) when user has no contact", async () => {
    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: buildContactsMap() }),
    );

    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
    });

    expect(result.success).toBe(true);
    expect(result.data?.pref).toBeUndefined();
    expect(inner.handleAsync).not.toHaveBeenCalled();
  });

  it("returns ok({ pref: undefined }) when contact entry exists but has no id", async () => {
    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: buildContactsMap([{ id: "" } as ContactDTO]) }),
    );

    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
    });

    expect(result.success).toBe(true);
    expect(result.data?.pref).toBeUndefined();
    expect(inner.handleAsync).not.toHaveBeenCalled();
  });

  // ---------------------------------------------------------------------------
  // Inner notFound (no prefs row) → ok({ pref: undefined })
  // ---------------------------------------------------------------------------

  it("collapses inner notFound (404) to ok({ pref: undefined })", async () => {
    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: buildContactsMap([fakeContact()]) }),
    );
    (inner.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(D2Result.notFound());

    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
    });

    expect(result.success).toBe(true);
    expect(result.data?.pref).toBeUndefined();
    expect(inner.handleAsync).toHaveBeenCalledWith({ contactId: CONTACT_ID });
  });

  // ---------------------------------------------------------------------------
  // Inner non-404 failures bubble up
  // ---------------------------------------------------------------------------

  it("bubbles non-404 failures from the inner handler", async () => {
    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: buildContactsMap([fakeContact()]) }),
    );
    (inner.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.fail({ messages: ["db error"], statusCode: 500 }),
    );

    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(500);
  });

  // ---------------------------------------------------------------------------
  // Happy path
  // ---------------------------------------------------------------------------

  it("returns the inner pref when both lookups succeed", async () => {
    const pref = buildPref({ emailEnabled: false, smsEnabled: true });

    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: buildContactsMap([fakeContact()]) }),
    );
    (inner.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: { pref } }),
    );

    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
    });

    expect(result.success).toBe(true);
    expect(result.data?.pref).toBe(pref);
    expect(inner.handleAsync).toHaveBeenCalledWith({ contactId: CONTACT_ID });
  });

  it("uses the first contact when multiple are returned for the same key", async () => {
    const first = fakeContact("aaaa-1111");
    const second = fakeContact("bbbb-2222");

    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: buildContactsMap([first, second]) }),
    );
    (inner.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: { pref: buildPref({ contactId: "aaaa-1111" }) } }),
    );

    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
    });

    expect(result.success).toBe(true);
    expect(inner.handleAsync).toHaveBeenCalledWith({ contactId: "aaaa-1111" });
  });
});
