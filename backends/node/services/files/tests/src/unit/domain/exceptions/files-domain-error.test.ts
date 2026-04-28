import { describe, it, expect } from "vitest";
import { FilesDomainError } from "@d2/files-domain";

describe("FilesDomainError", () => {
  it("should set the message correctly", () => {
    const error = new FilesDomainError("something went wrong");
    expect(error.message).toBe("something went wrong");
  });

  it("should set name to FilesDomainError", () => {
    const error = new FilesDomainError("test");
    expect(error.name).toBe("FilesDomainError");
  });

  it("should be an instance of Error", () => {
    const error = new FilesDomainError("test");
    expect(error).toBeInstanceOf(Error);
  });

  it("should support error cause via options", () => {
    const cause = new Error("root cause");
    const error = new FilesDomainError("wrapper", { cause });
    expect(error.cause).toBe(cause);
  });
});
