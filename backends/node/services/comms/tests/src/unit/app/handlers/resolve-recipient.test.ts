import { describe, it, expect, vi } from "vitest";
import { D2Result } from "@d2/result";
import { RecipientResolver } from "@d2/comms-app";
import { createMockContext } from "../helpers/mock-handlers.js";

const CONTACT_ID = "01234567-89ab-7def-8123-456789abcdef";
const CONTACT_ID_2 = "01234567-89ab-7def-8123-456789abcde0";
const CONTACT_ID_3 = "01234567-89ab-7def-8123-456789abcde1";

describe("RecipientResolver", () => {
  it("should resolve email and phone from contactId", async () => {
    const contactMap = new Map();
    contactMap.set(CONTACT_ID, {
      id: CONTACT_ID,
      contextKey: "auth_org_invitation",
      relatedEntityId: "inv-1",
      contactMethods: {
        emails: [{ value: "invitee@example.com", labels: [] }],
        phoneNumbers: [{ value: "+15559876543", labels: [] }],
      },
      personalDetails: undefined,
      professionalDetails: undefined,
      location: undefined,
      createdAt: new Date(),
    });

    const geoIds = {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { data: contactMap } })),
    };

    const resolver = new RecipientResolver(geoIds as any, createMockContext());
    const result = await resolver.handleAsync({ contactId: CONTACT_ID });

    expect(result.success).toBe(true);
    expect(result.data!.email).toBe("invitee@example.com");
    expect(result.data!.phone).toBe("+15559876543");
    expect(geoIds.handleAsync).toHaveBeenCalledWith({ ids: [CONTACT_ID] });
  });

  it("should propagate notFound when geo returns NOT_FOUND", async () => {
    const geoIds = {
      handleAsync: vi.fn().mockResolvedValue(D2Result.notFound()),
    };

    const resolver = new RecipientResolver(geoIds as any, createMockContext());
    const result = await resolver.handleAsync({ contactId: CONTACT_ID });

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe("NOT_FOUND");
  });

  it("should return notFound when contact found but not in result map", async () => {
    // Edge case: geo returns ok with empty map (e.g. empty IDs were requested)
    const geoIds = {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { data: new Map() } })),
    };

    const resolver = new RecipientResolver(geoIds as any, createMockContext());
    const result = await resolver.handleAsync({ contactId: CONTACT_ID });

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe("NOT_FOUND");
  });

  it("should propagate geo failure via bubbleFail", async () => {
    const geoIds = {
      handleAsync: vi
        .fn()
        .mockResolvedValue(D2Result.fail({ messages: ["Geo timeout"], statusCode: 503 })),
    };

    const resolver = new RecipientResolver(geoIds as any, createMockContext());
    const result = await resolver.handleAsync({ contactId: CONTACT_ID });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
    expect(result.messages).toContain("Geo timeout");
  });

  it("should propagate geo 400 failure without masking status code", async () => {
    const geoIds = {
      handleAsync: vi
        .fn()
        .mockResolvedValue(
          D2Result.fail({ messages: ["Invalid contact ID format"], statusCode: 400 }),
        ),
    };

    const resolver = new RecipientResolver(geoIds as any, createMockContext());
    const result = await resolver.handleAsync({ contactId: CONTACT_ID });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
    expect(result.messages).toContain("Invalid contact ID format");
  });

  // -------------------------------------------------------------------------
  // Defensive edge cases
  // -------------------------------------------------------------------------

  it("should return validation failure for empty string contactId", async () => {
    const geoIds = { handleAsync: vi.fn() };
    const resolver = new RecipientResolver(geoIds as any, createMockContext());
    const result = await resolver.handleAsync({ contactId: "" });

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe("VALIDATION_FAILED");
    // Should NOT call geo
    expect(geoIds.handleAsync).not.toHaveBeenCalled();
  });

  it("should handle contact with empty emails and phoneNumbers arrays", async () => {
    const contactMap = new Map();
    contactMap.set(CONTACT_ID_2, {
      id: CONTACT_ID_2,
      contextKey: "auth_user",
      relatedEntityId: "user-empty",
      contactMethods: {
        emails: [],
        phoneNumbers: [],
      },
      personalDetails: undefined,
      professionalDetails: undefined,
      location: undefined,
      createdAt: new Date(),
    });

    const geoIds = {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { data: contactMap } })),
    };

    const resolver = new RecipientResolver(geoIds as any, createMockContext());
    const result = await resolver.handleAsync({ contactId: CONTACT_ID_2 });

    expect(result.success).toBe(true);
    expect(result.data!.email).toBeUndefined();
    expect(result.data!.phone).toBeUndefined();
  });

  it("should handle contact with undefined contactMethods", async () => {
    const contactMap = new Map();
    contactMap.set(CONTACT_ID_3, {
      id: CONTACT_ID_3,
      contextKey: "auth_user",
      relatedEntityId: "user-no-methods",
      contactMethods: undefined,
      personalDetails: undefined,
      professionalDetails: undefined,
      location: undefined,
      createdAt: new Date(),
    });

    const geoIds = {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { data: contactMap } })),
    };

    const resolver = new RecipientResolver(geoIds as any, createMockContext());
    const result = await resolver.handleAsync({ contactId: CONTACT_ID_3 });

    expect(result.success).toBe(true);
    expect(result.data!.email).toBeUndefined();
    expect(result.data!.phone).toBeUndefined();
  });

  it("should define redaction spec that redacts PII output fields", () => {
    const geoIds = { handleAsync: vi.fn() };
    const resolver = new RecipientResolver(geoIds as any, createMockContext());
    expect(resolver.redaction).toBeDefined();
    expect(resolver.redaction?.outputFields).toContain("email");
    expect(resolver.redaction?.outputFields).toContain("phone");
  });
});
