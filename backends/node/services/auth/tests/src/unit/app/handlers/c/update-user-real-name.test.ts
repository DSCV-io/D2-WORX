import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode } from "@d2/result";
import { UpdateUserRealName } from "@d2/auth-app";
import type { IUpdateUserNameHandler } from "@d2/auth-app";
import type { Complex } from "@d2/geo-client";

const VALID_USER_ID = "01234567-89ab-cdef-0123-456789abcdef";

function createTestContext() {
  const request: IRequestContext = {
    traceId: "trace-test",
    isAuthenticated: true,
    isTrustedService: false,
    isOrgEmulating: false,
    isUserImpersonating: false,
    isAgentStaff: false,
    isAgentAdmin: false,
    isTargetingStaff: false,
    isTargetingAdmin: false,
  };
  return new HandlerContext(request, createLogger({ level: "silent" as never }));
}

function createMockUpdateContactsByExtKeys(): Complex.IUpdateContactsByExtKeysHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(
      D2Result.ok({
        data: {
          replacements: [{ newContact: { id: "geo-001" } }],
        },
      }),
    ),
  } as unknown as Complex.IUpdateContactsByExtKeysHandler;
}

function createMockUpdateUserName() {
  return { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })) };
}

describe("UpdateUserRealName", () => {
  let updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler;
  let updateUserName: ReturnType<typeof createMockUpdateUserName>;
  let handler: UpdateUserRealName;

  beforeEach(() => {
    updateContactsByExtKeys = createMockUpdateContactsByExtKeys();
    updateUserName = createMockUpdateUserName();
    handler = new UpdateUserRealName(
      updateContactsByExtKeys,
      updateUserName as unknown as IUpdateUserNameHandler,
      createTestContext(),
    );
  });

  // -----------------------------------------------------------------------
  // Validation (Zod schema)
  // -----------------------------------------------------------------------

  it("should return validationFailed when userId is not a valid UUID", async () => {
    const result = await handler.handleAsync({
      userId: "not-a-uuid",
      firstName: "John",
      lastName: "Doe",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(updateContactsByExtKeys.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when firstName is empty", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      firstName: "",
      lastName: "Doe",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(updateContactsByExtKeys.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when lastName is empty", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      firstName: "John",
      lastName: "",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(updateContactsByExtKeys.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when firstName exceeds 255 chars", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      firstName: "x".repeat(256),
      lastName: "Doe",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  // -----------------------------------------------------------------------
  // cleanDisplayStr validation
  // -----------------------------------------------------------------------

  it("should return inputError on firstName when it contains only invalid chars", async () => {
    // All chars stripped by cleanDisplayStr → empty → validation fails
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      firstName: "<>[](){}$`~",
      lastName: "Doe",
    });

    expect(result.success).toBe(false);
    expect(result.inputErrors.length).toBeGreaterThanOrEqual(1);
    expect(result.inputErrors[0][0]).toBe("firstName");
  });

  it("should return inputError on lastName when it is whitespace only", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      firstName: "John",
      lastName: "   ",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  // -----------------------------------------------------------------------
  // Geo failure (cross-service — should NOT bubbleFail)
  // -----------------------------------------------------------------------

  it("should return serviceUnavailable when Geo UpdateContactsByExtKeys fails", async () => {
    updateContactsByExtKeys.handleAsync = vi.fn().mockResolvedValue(
      D2Result.fail({
        messages: ["internal geo error with sensitive details"],
        statusCode: HttpStatusCode.InternalServerError,
      }),
    );

    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      firstName: "John",
      lastName: "Doe",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    // Should NOT forward Geo's internal error message
    expect(result.messages).not.toContain("internal geo error with sensitive details");
    expect(updateUserName.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Repo failure (same-service — bubbleFail is safe)
  // -----------------------------------------------------------------------

  it("should bubble failure when repo updateUserName returns notFound", async () => {
    updateUserName.handleAsync = vi.fn().mockResolvedValue(D2Result.notFound());

    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      firstName: "John",
      lastName: "Doe",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.NotFound);
    expect(updateContactsByExtKeys.handleAsync).toHaveBeenCalledOnce();
  });

  // -----------------------------------------------------------------------
  // Success
  // -----------------------------------------------------------------------

  it("should return combined name on success", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      firstName: "John",
      lastName: "Doe",
    });

    expect(result.success).toBe(true);
    expect(result.data?.name).toBe("John Doe");
  });

  it("should call Geo with auth_user context key and userId as relatedEntityId", async () => {
    await handler.handleAsync({
      userId: VALID_USER_ID,
      firstName: "Jane",
      lastName: "Smith",
    });

    const call = vi.mocked(updateContactsByExtKeys.handleAsync).mock.calls[0][0];
    expect(call.contacts).toHaveLength(1);
    expect(call.contacts[0].contextKey).toBe("auth_user");
    expect(call.contacts[0].relatedEntityId).toBe(VALID_USER_ID);
    expect(call.contacts[0].personalDetails?.firstName).toBe("Jane");
    expect(call.contacts[0].personalDetails?.lastName).toBe("Smith");
  });

  it("should update BetterAuth user.name with combined firstName + lastName", async () => {
    await handler.handleAsync({
      userId: VALID_USER_ID,
      firstName: "Jane",
      lastName: "Smith",
    });

    expect(updateUserName.handleAsync).toHaveBeenCalledWith({
      userId: VALID_USER_ID,
      name: "Jane Smith",
    });
  });

  it("should clean display names (strip dangerous chars, normalize whitespace)", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      firstName: "  John  ",
      lastName: "  O'Brien  ",
    });

    expect(result.success).toBe(true);
    expect(result.data?.name).toBe("John O'Brien");
  });

  it("should define redaction spec with firstName and lastName input fields", () => {
    expect(handler.redaction).toBeDefined();
    expect(handler.redaction?.inputFields).toContain("firstName");
    expect(handler.redaction?.inputFields).toContain("lastName");
  });
});
