import { describe, it, expect } from "vitest";
import { CommsValidationError, CommsDomainError } from "@d2/comms-domain";

describe("CommsValidationError", () => {
  it("should format the message correctly", () => {
    const error = new CommsValidationError("Message", "content", "bad", "is required.");
    expect(error.message).toBe("Validation failed for Message.content: is required.");
  });

  it("should store structured properties", () => {
    const error = new CommsValidationError("Thread", "slug", "", "cannot be empty.");
    expect(error.entityName).toBe("Thread");
    expect(error.propertyName).toBe("slug");
    expect(error.invalidValue).toBe("");
    expect(error.reason).toBe("cannot be empty.");
  });

  it("should handle null invalidValue in message", () => {
    const error = new CommsValidationError("Message", "sender", null, "is required.");
    expect(error.message).toBe("Validation failed for Message.sender: is required.");
    expect(error.invalidValue).toBeNull();
  });

  it("should handle undefined invalidValue in message", () => {
    const error = new CommsValidationError("Message", "content", undefined, "is required.");
    expect(error.message).toBe("Validation failed for Message.content: is required.");
    expect(error.invalidValue).toBeUndefined();
  });

  it("should set name to CommsValidationError", () => {
    const error = new CommsValidationError("Message", "content", "x", "invalid");
    expect(error.name).toBe("CommsValidationError");
  });

  it("should be an instance of CommsDomainError", () => {
    const error = new CommsValidationError("Message", "content", "x", "invalid");
    expect(error).toBeInstanceOf(CommsDomainError);
  });

  it("should be an instance of Error", () => {
    const error = new CommsValidationError("Message", "content", "x", "invalid");
    expect(error).toBeInstanceOf(Error);
  });
});
