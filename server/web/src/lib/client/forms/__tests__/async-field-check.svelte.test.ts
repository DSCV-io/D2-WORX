import { describe, it, expect, vi, beforeEach } from "vitest";
import { writable } from "svelte/store";
import { flushSync } from "svelte";
import { useAsyncFieldCheck } from "../async-field-check.svelte.js";

function createMockForm(
  formData: Record<string, string> = {},
  errorsData: Record<string, string[]> = {},
) {
  return {
    form: writable({ email: "", ...formData }),
    errors: writable({ ...errorsData }),
  };
}

function createPassingChecker() {
  return vi.fn().mockResolvedValue({ valid: true });
}

function createFailingChecker(msg = "Already taken") {
  return vi.fn().mockResolvedValue({ valid: false, errorMessage: msg });
}

describe("useAsyncFieldCheck", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("starts with idle status", () => {
    const form = createMockForm();
    const result = useAsyncFieldCheck({
      form,
      field: "email",
      checker: createPassingChecker(),
    });

    expect(result.status).toBe("idle");
  });

  it("transitions to valid when checker returns valid", async () => {
    const form = createMockForm({ email: "test@example.com" });
    const checker = createPassingChecker();
    const result = useAsyncFieldCheck({ form, field: "email", checker });

    await result.check();

    expect(result.status).toBe("valid");
    expect(checker).toHaveBeenCalledWith("test@example.com");
  });

  it("transitions to invalid and sets error when checker returns invalid", async () => {
    const form = createMockForm({ email: "taken@example.com" });
    const checker = createFailingChecker("This email is already taken");
    const result = useAsyncFieldCheck({ form, field: "email", checker });

    let currentErrors: Record<string, string[]> = {};
    form.errors.subscribe((e) => {
      currentErrors = e as Record<string, string[]>;
    });

    await result.check();

    expect(result.status).toBe("invalid");
    expect(currentErrors.email).toEqual(["This email is already taken"]);
  });

  it("uses default error message when errorMessage is not provided", async () => {
    const form = createMockForm({ email: "bad@example.com" });
    const checker = vi.fn().mockResolvedValue({ valid: false });
    const result = useAsyncFieldCheck({ form, field: "email", checker });

    let currentErrors: Record<string, string[]> = {};
    form.errors.subscribe((e) => {
      currentErrors = e as Record<string, string[]>;
    });

    await result.check();

    expect(currentErrors.email).toEqual(["Validation failed."]);
  });

  it("skips check when field value is falsy (default preCheck)", async () => {
    const form = createMockForm({ email: "" });
    const checker = createPassingChecker();
    const result = useAsyncFieldCheck({ form, field: "email", checker });

    await result.check();

    expect(result.status).toBe("idle");
    expect(checker).not.toHaveBeenCalled();
  });

  it("skips check when custom preCheck returns false", async () => {
    const form = createMockForm({ email: "no-at-sign" });
    const checker = createPassingChecker();
    const result = useAsyncFieldCheck({
      form,
      field: "email",
      checker,
      preCheck: (v) => !!v && v.includes("@"),
    });

    await result.check();

    expect(result.status).toBe("idle");
    expect(checker).not.toHaveBeenCalled();
  });

  it("runs check when custom preCheck returns true", async () => {
    const form = createMockForm({ email: "test@example.com" });
    const checker = createPassingChecker();
    const result = useAsyncFieldCheck({
      form,
      field: "email",
      checker,
      preCheck: (v) => !!v && v.includes("@"),
    });

    await result.check();

    expect(result.status).toBe("valid");
    expect(checker).toHaveBeenCalledWith("test@example.com");
  });

  it("skips check when client-side validation errors already exist", async () => {
    const form = createMockForm({ email: "bad" }, { email: ["Invalid email format"] });
    const checker = createPassingChecker();
    const result = useAsyncFieldCheck({ form, field: "email", checker });

    await result.check();

    expect(result.status).toBe("idle");
    expect(checker).not.toHaveBeenCalled();
  });

  it("resets status to idle on reset()", async () => {
    const form = createMockForm({ email: "test@example.com" });
    const checker = createPassingChecker();
    const result = useAsyncFieldCheck({ form, field: "email", checker });

    await result.check();
    expect(result.status).toBe("valid");

    flushSync(() => {
      result.reset();
    });
    expect(result.status).toBe("idle");
  });

  it("resets from invalid to idle", async () => {
    const form = createMockForm({ email: "taken@example.com" });
    const checker = createFailingChecker();
    const result = useAsyncFieldCheck({ form, field: "email", checker });

    await result.check();
    expect(result.status).toBe("invalid");

    flushSync(() => {
      result.reset();
    });
    expect(result.status).toBe("idle");
  });

  it("reverts to idle on checker exception", async () => {
    const form = createMockForm({ email: "test@example.com" });
    const checker = vi.fn().mockRejectedValue(new Error("Network error"));
    const result = useAsyncFieldCheck({ form, field: "email", checker });

    await result.check();

    expect(result.status).toBe("idle");
  });

  it("works with a non-email field", async () => {
    const formStore = writable({ username: "testuser" });
    const errorsStore = writable({});
    const form = { form: formStore, errors: errorsStore };
    const checker = createPassingChecker();

    const result = useAsyncFieldCheck({
      form,
      field: "username",
      checker,
    });

    await result.check();

    expect(result.status).toBe("valid");
    expect(checker).toHaveBeenCalledWith("testuser");
  });

  it("can run check→reset→check cycle", async () => {
    const form = createMockForm({ email: "test@example.com" });
    const checker = createPassingChecker();
    const result = useAsyncFieldCheck({ form, field: "email", checker });

    await result.check();
    expect(result.status).toBe("valid");

    flushSync(() => {
      result.reset();
    });
    expect(result.status).toBe("idle");

    await result.check();
    expect(result.status).toBe("valid");
    expect(checker).toHaveBeenCalledTimes(2);
  });

  it("does not check fields with empty errors array", async () => {
    // Empty array = no errors, should proceed with check
    const form = createMockForm({ email: "test@example.com" });
    form.errors.set({ email: [] });
    const checker = createPassingChecker();
    const result = useAsyncFieldCheck({ form, field: "email", checker });

    await result.check();

    expect(checker).toHaveBeenCalled();
    expect(result.status).toBe("valid");
  });
});
