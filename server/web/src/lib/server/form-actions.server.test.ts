import { describe, it, expect, vi } from "vitest";
import { z } from "zod";
import { D2Result, type HttpStatusCode } from "@d2/result";

// Mock sveltekit-superforms — return a minimal SuperValidated shape
vi.mock("sveltekit-superforms", () => {
  const fail = (status: number, data: Record<string, unknown>) => ({ status, ...data });
  const message = (form: unknown, msg: string, opts?: { status?: number }) => ({
    form,
    message: msg,
    status: opts?.status,
  });
  const superValidate = vi.fn();
  return { fail, message, superValidate };
});

vi.mock("sveltekit-superforms/adapters", () => ({
  zod4: (schema: unknown) => schema,
}));

vi.mock("$lib/shared/forms/form-helpers.js", () => ({
  applyD2Errors: vi.fn(),
}));

import { validateAndSubmit } from "./form-actions.server.js";
import { superValidate } from "sveltekit-superforms";
import { applyD2Errors } from "$lib/shared/forms/form-helpers.js";

const mockSuperValidate = vi.mocked(superValidate);
const mockApplyD2Errors = vi.mocked(applyD2Errors);

const testSchema = z.object({
  email: z.string().min(1),
  name: z.string().min(1),
});

function fakeRequest(): Request {
  return new Request("http://localhost/test", { method: "POST" });
}

describe("validateAndSubmit", () => {
  it("returns 400 with form errors when validation fails", async () => {
    mockSuperValidate.mockResolvedValue({
      valid: false,
      data: { email: "", name: "" },
      errors: { email: ["Required"] },
    } as never);

    const submit = vi.fn();
    const result = await validateAndSubmit({
      request: fakeRequest(),
      schema: testSchema,
      submit,
    });

    expect(submit).not.toHaveBeenCalled();
    expect(result).toHaveProperty("status", 400);
  });

  it("calls submit with validated data when form is valid", async () => {
    const formData = { email: "test@example.com", name: "Alice" };
    mockSuperValidate.mockResolvedValue({
      valid: true,
      data: formData,
      errors: {},
    } as never);

    const submit = vi.fn().mockResolvedValue(D2Result.ok());

    await validateAndSubmit({
      request: fakeRequest(),
      schema: testSchema,
      submit,
    });

    expect(submit).toHaveBeenCalledWith(formData);
  });

  it("maps D2Result inputErrors to Superforms field errors", async () => {
    mockSuperValidate.mockResolvedValue({
      valid: true,
      data: { email: "test@example.com", name: "Alice" },
      errors: {},
    } as never);

    const d2result = D2Result.validationFailed({
      inputErrors: [["email", "Already taken"]],
    });

    const submit = vi.fn().mockResolvedValue(d2result);

    const result = await validateAndSubmit({
      request: fakeRequest(),
      schema: testSchema,
      submit,
    });

    expect(mockApplyD2Errors).toHaveBeenCalled();
    expect(result).toHaveProperty("status", 400);
  });

  it("returns form-level message on D2Result failure without inputErrors", async () => {
    mockSuperValidate.mockResolvedValue({
      valid: true,
      data: { email: "test@example.com", name: "Alice" },
      errors: {},
    } as never);

    const d2result = D2Result.fail({
      messages: ["Something went wrong"],
      statusCode: 500 as HttpStatusCode,
    });

    const submit = vi.fn().mockResolvedValue(d2result);

    const result = await validateAndSubmit({
      request: fakeRequest(),
      schema: testSchema,
      submit,
    });

    expect(result).toHaveProperty("message", "Something went wrong");
    expect(result).toHaveProperty("status", 500);
  });

  it("returns form with success message when provided", async () => {
    mockSuperValidate.mockResolvedValue({
      valid: true,
      data: { email: "test@example.com", name: "Alice" },
      errors: {},
    } as never);

    const submit = vi.fn().mockResolvedValue(D2Result.ok());

    const result = await validateAndSubmit({
      request: fakeRequest(),
      schema: testSchema,
      submit,
      successMessage: "All good!",
    });

    expect(result).toHaveProperty("message", "All good!");
  });

  it("returns form without message on success when no successMessage", async () => {
    mockSuperValidate.mockResolvedValue({
      valid: true,
      data: { email: "test@example.com", name: "Alice" },
      errors: {},
    } as never);

    const submit = vi.fn().mockResolvedValue(D2Result.ok());

    const result = await validateAndSubmit({
      request: fakeRequest(),
      schema: testSchema,
      submit,
    });

    expect(result).toHaveProperty("form");
    expect(result).not.toHaveProperty("message");
  });

  it("joins multiple D2Result messages with period separator", async () => {
    mockSuperValidate.mockResolvedValue({
      valid: true,
      data: { email: "test@example.com", name: "Alice" },
      errors: {},
    } as never);

    const d2result = D2Result.fail({
      messages: ["Error one", "Error two"],
      statusCode: 400 as HttpStatusCode,
    });

    const submit = vi.fn().mockResolvedValue(d2result);

    const result = await validateAndSubmit({
      request: fakeRequest(),
      schema: testSchema,
      submit,
    });

    expect(result).toHaveProperty("message", "Error one. Error two");
  });

  it("uses fallback message when D2Result has no messages", async () => {
    mockSuperValidate.mockResolvedValue({
      valid: true,
      data: { email: "test@example.com", name: "Alice" },
      errors: {},
    } as never);

    // D2Result.fail() defaults messages to []. join(". ") = "" (falsy).
    // The || operator catches both undefined and empty string → fallback.
    const d2result = D2Result.fail({
      statusCode: 500 as HttpStatusCode,
    });

    const submit = vi.fn().mockResolvedValue(d2result);

    const result = await validateAndSubmit({
      request: fakeRequest(),
      schema: testSchema,
      submit,
    });

    expect(result).toHaveProperty("message", "common_errors_unknown");
  });

  it("uses D2Result statusCode for inputError failures", async () => {
    mockSuperValidate.mockResolvedValue({
      valid: true,
      data: { email: "test@example.com", name: "Alice" },
      errors: {},
    } as never);

    const d2result = new D2Result({
      success: false,
      statusCode: 409 as HttpStatusCode,
      inputErrors: [["email", "Conflict"]],
    });

    const submit = vi.fn().mockResolvedValue(d2result);

    const result = await validateAndSubmit({
      request: fakeRequest(),
      schema: testSchema,
      submit,
    });

    expect(result).toHaveProperty("status", 409);
  });
});
