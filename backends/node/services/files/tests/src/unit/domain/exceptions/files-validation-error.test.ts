import { describe, it, expect } from "vitest";
import { FilesValidationError, FilesDomainError } from "@d2/files-domain";

describe("FilesValidationError", () => {
  it("should format the message correctly", () => {
    const error = new FilesValidationError("File", "contentType", "bad", "is required.");
    expect(error.message).toBe(
      "Validation failed for File.contentType with value 'bad': is required.",
    );
  });

  it("should store structured properties", () => {
    const error = new FilesValidationError("File", "displayName", "", "cannot be empty.");
    expect(error.entityName).toBe("File");
    expect(error.propertyName).toBe("displayName");
    expect(error.invalidValue).toBe("");
    expect(error.reason).toBe("cannot be empty.");
  });

  it("should handle null invalidValue in message", () => {
    const error = new FilesValidationError("File", "contextKey", null, "is required.");
    expect(error.message).toBe(
      "Validation failed for File.contextKey with value 'null': is required.",
    );
    expect(error.invalidValue).toBeNull();
  });

  it("should handle undefined invalidValue in message", () => {
    const error = new FilesValidationError("File", "displayName", undefined, "is required.");
    expect(error.message).toContain("'undefined'");
    expect(error.invalidValue).toBeUndefined();
  });

  it("should set name to FilesValidationError", () => {
    const error = new FilesValidationError("File", "sizeBytes", 0, "invalid");
    expect(error.name).toBe("FilesValidationError");
  });

  it("should be an instance of FilesDomainError", () => {
    const error = new FilesValidationError("File", "sizeBytes", 0, "invalid");
    expect(error).toBeInstanceOf(FilesDomainError);
  });

  it("should be an instance of Error", () => {
    const error = new FilesValidationError("File", "sizeBytes", 0, "invalid");
    expect(error).toBeInstanceOf(Error);
  });
});
