import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { D2Result, HttpStatusCode } from "@d2/result";
import { UpdateUsername } from "@d2/auth-app";
import type { ICheckUsernameAvailableHandler, IUpdateUserUsernameHandler } from "@d2/auth-app";

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

function createMockCheckAvailable(available = true) {
  return { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { available } })) };
}

function createMockUpdateUsername() {
  return { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })) };
}

describe("UpdateUsername", () => {
  let checkAvailable: ReturnType<typeof createMockCheckAvailable>;
  let updateUsernameRepo: ReturnType<typeof createMockUpdateUsername>;
  let handler: UpdateUsername;

  beforeEach(() => {
    checkAvailable = createMockCheckAvailable();
    updateUsernameRepo = createMockUpdateUsername();
    handler = new UpdateUsername(
      checkAvailable as unknown as ICheckUsernameAvailableHandler,
      updateUsernameRepo as unknown as IUpdateUserUsernameHandler,
      createTestContext(),
    );
  });

  // -----------------------------------------------------------------------
  // Validation (Zod schema)
  // -----------------------------------------------------------------------

  it("should return validationFailed when userId is not a valid UUID", async () => {
    const result = await handler.handleAsync({
      userId: "not-a-uuid",
      username: "johndoe",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(checkAvailable.handleAsync).not.toHaveBeenCalled();
  });

  it("should return validationFailed when username is empty", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  it("should return validationFailed when username exceeds 32 chars", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "a".repeat(33),
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  // -----------------------------------------------------------------------
  // Alphanumeric-only validation
  // -----------------------------------------------------------------------

  it("should return inputError when username contains special chars", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "john.doe",
    });

    expect(result.success).toBe(false);
    expect(result.inputErrors.length).toBeGreaterThanOrEqual(1);
    expect(result.inputErrors[0][0]).toBe("username");
    expect(result.inputErrors[0][1]).toContain("letters and numbers");
    expect(checkAvailable.handleAsync).not.toHaveBeenCalled();
  });

  it("should return inputError when username contains spaces", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "john doe",
    });

    expect(result.success).toBe(false);
    expect(result.inputErrors[0][0]).toBe("username");
    expect(checkAvailable.handleAsync).not.toHaveBeenCalled();
  });

  it("should return inputError when username contains underscores", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "john_doe",
    });

    expect(result.success).toBe(false);
    expect(result.inputErrors[0][0]).toBe("username");
  });

  it("should return inputError when username contains hyphens", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "john-doe",
    });

    expect(result.success).toBe(false);
    expect(result.inputErrors[0][0]).toBe("username");
  });

  it("should return inputError when username is too short", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "ab",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  it("should return inputError when username is whitespace only", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "   ",
    });

    expect(result.success).toBe(false);
    expect(result.inputErrors[0][0]).toBe("username");
  });

  // -----------------------------------------------------------------------
  // Uniqueness check
  // -----------------------------------------------------------------------

  it("should return inputError when username is already taken", async () => {
    checkAvailable = createMockCheckAvailable(false);
    handler = new UpdateUsername(
      checkAvailable as unknown as ICheckUsernameAvailableHandler,
      updateUsernameRepo as unknown as IUpdateUserUsernameHandler,
      createTestContext(),
    );

    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "takenuser",
    });

    expect(result.success).toBe(false);
    expect(result.inputErrors.length).toBeGreaterThanOrEqual(1);
    expect(result.inputErrors[0][0]).toBe("username");
    expect(result.inputErrors[0][1]).toContain("already taken");
    expect(updateUsernameRepo.handleAsync).not.toHaveBeenCalled();
  });

  it("should bubble failure when checkAvailable returns error", async () => {
    checkAvailable.handleAsync = vi.fn().mockResolvedValue(
      D2Result.fail({
        messages: ["DB connection lost"],
        statusCode: HttpStatusCode.InternalServerError,
      }),
    );

    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "johndoe",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.InternalServerError);
    expect(updateUsernameRepo.handleAsync).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // Repo failure
  // -----------------------------------------------------------------------

  it("should bubble failure when repo updateUsername returns notFound", async () => {
    updateUsernameRepo.handleAsync = vi.fn().mockResolvedValue(D2Result.notFound());

    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "johndoe",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.NotFound);
  });

  // -----------------------------------------------------------------------
  // Success
  // -----------------------------------------------------------------------

  it("should return lowercased username and original displayUsername on success", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "JohnDoe",
    });

    expect(result.success).toBe(true);
    expect(result.data?.username).toBe("johndoe");
    expect(result.data?.displayUsername).toBe("JohnDoe");
  });

  it("should pass lowercased username to uniqueness check", async () => {
    await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "JohnDoe",
    });

    expect(checkAvailable.handleAsync).toHaveBeenCalledWith({ username: "johndoe" });
  });

  it("should pass both username and displayUsername to repo update", async () => {
    await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "JohnDoe",
    });

    expect(updateUsernameRepo.handleAsync).toHaveBeenCalledWith({
      userId: VALID_USER_ID,
      username: "johndoe",
      displayUsername: "JohnDoe",
    });
  });

  it("should accept valid alphanumeric username at max length", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "a".repeat(32),
    });

    expect(result.success).toBe(true);
  });

  it("should accept username with mixed case letters and digits", async () => {
    const result = await handler.handleAsync({
      userId: VALID_USER_ID,
      username: "User123ABC",
    });

    expect(result.success).toBe(true);
    expect(result.data?.username).toBe("user123abc");
    expect(result.data?.displayUsername).toBe("User123ABC");
  });

  it("should define redaction spec with username input field", () => {
    expect(handler.redaction).toBeDefined();
    expect(handler.redaction?.inputFields).toContain("username");
  });
});
