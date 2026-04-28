import { describe, it, expect, beforeEach, vi } from "vitest";
import { D2Result } from "@d2/result";
import { SetUserChannelPreference } from "@d2/comms-app";
import type { ChannelPreference } from "@d2/comms-domain";
import type { IGetContactsByExtKeysHandler, GetContactsByExtKeysOutput } from "@d2/geo-client";
import type { ContactDTO } from "@d2/protos";
import type { Commands } from "@d2/comms-app";
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

function createMockInner(): Commands.ISetChannelPreferenceHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { pref: buildPref() } })),
  } as unknown as Commands.ISetChannelPreferenceHandler;
}

describe("SetUserChannelPreference", () => {
  let geo: IGetContactsByExtKeysHandler;
  let inner: Commands.ISetChannelPreferenceHandler;
  let handler: SetUserChannelPreference;

  beforeEach(() => {
    geo = createMockGetContactsByExtKeys();
    inner = createMockInner();
    handler = new SetUserChannelPreference(geo, inner, createMockContext());
  });

  // ---------------------------------------------------------------------------
  // Validation
  // ---------------------------------------------------------------------------

  it("rejects empty contextKey", async () => {
    const result = await handler.handleAsync({
      contextKey: "",
      relatedEntityId: RELATED_ENTITY_ID,
      emailEnabled: true,
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
      smsEnabled: false,
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("rejects contextKey exceeding 64 chars", async () => {
    const result = await handler.handleAsync({
      contextKey: "a".repeat(65),
      relatedEntityId: RELATED_ENTITY_ID,
      emailEnabled: true,
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("rejects relatedEntityId exceeding 64 chars", async () => {
    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: "a".repeat(65),
      smsEnabled: true,
    });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  // ---------------------------------------------------------------------------
  // Geo lookup failures → SERVICE_UNAVAILABLE
  // ---------------------------------------------------------------------------

  it("returns 503 when Geo lookup fails", async () => {
    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.fail({ messages: ["geo down"], statusCode: 500 }),
    );

    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
      emailEnabled: true,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
    expect(inner.handleAsync).not.toHaveBeenCalled();
  });

  // ---------------------------------------------------------------------------
  // No contact yet → 404 (cannot attach prefs)
  // ---------------------------------------------------------------------------

  it("returns 404 when user has no contact", async () => {
    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: buildContactsMap() }),
    );

    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
      emailEnabled: true,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
    expect(inner.handleAsync).not.toHaveBeenCalled();
  });

  it("returns 404 when contact entry exists but has no id", async () => {
    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: buildContactsMap([{ id: "" } as ContactDTO]) }),
    );

    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
      smsEnabled: false,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(404);
    expect(inner.handleAsync).not.toHaveBeenCalled();
  });

  // ---------------------------------------------------------------------------
  // Inner failures bubble up
  // ---------------------------------------------------------------------------

  it("bubbles inner handler failures", async () => {
    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: buildContactsMap([fakeContact()]) }),
    );
    (inner.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.fail({ messages: ["db down"], statusCode: 500 }),
    );

    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
      emailEnabled: true,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(500);
  });

  // ---------------------------------------------------------------------------
  // Happy path — fields forwarded correctly to inner handler
  // ---------------------------------------------------------------------------

  it("forwards both emailEnabled and smsEnabled to the inner handler", async () => {
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
      emailEnabled: false,
      smsEnabled: true,
    });

    expect(result.success).toBe(true);
    expect(result.data?.pref).toBe(pref);
    expect(inner.handleAsync).toHaveBeenCalledWith({
      contactId: CONTACT_ID,
      emailEnabled: false,
      smsEnabled: true,
    });
  });

  it("forwards a single-field partial update without setting the other", async () => {
    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: buildContactsMap([fakeContact()]) }),
    );

    await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
      emailEnabled: true,
    });

    const callArgs = (inner.handleAsync as ReturnType<typeof vi.fn>).mock.calls[0][0];
    expect(callArgs).toEqual({
      contactId: CONTACT_ID,
      emailEnabled: true,
      smsEnabled: undefined,
    });
  });

  it("uses the first contact when multiple are returned for the same key", async () => {
    const first = fakeContact("aaaa-1111");
    const second = fakeContact("bbbb-2222");

    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: buildContactsMap([first, second]) }),
    );

    await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
      smsEnabled: true,
    });

    const callArgs = (inner.handleAsync as ReturnType<typeof vi.fn>).mock.calls[0][0];
    expect(callArgs.contactId).toBe("aaaa-1111");
  });

  // ---------------------------------------------------------------------------
  // Inner ok-but-empty-data → bubbleFail (defensive)
  // ---------------------------------------------------------------------------

  it("bubbles when inner returns ok with no pref data", async () => {
    (geo.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: buildContactsMap([fakeContact()]) }),
    );
    (inner.handleAsync as ReturnType<typeof vi.fn>).mockResolvedValue(
      D2Result.ok({ data: undefined }) as never,
    );

    const result = await handler.handleAsync({
      contextKey: CONTEXT_KEY,
      relatedEntityId: RELATED_ENTITY_ID,
      emailEnabled: true,
    });

    // Handler defensively bubbleFails on empty data even if inner reports success
    expect(result.success).toBe(false);
  });
});
