/**
 * Server-side form action helpers for Superforms + D2Result integration.
 *
 * Provides a `validateAndSubmit` helper that:
 * 1. Validates the request body against a Zod schema via Superforms
 * 2. Calls a submit function with the validated data
 * 3. Maps D2Result errors back to Superforms field errors
 */
import { type NumericRange, type RequestEvent } from "@sveltejs/kit";
import { fail, superValidate, message } from "sveltekit-superforms";
import { zod4 as zod } from "sveltekit-superforms/adapters";
import type { z } from "zod";
import { TK } from "@d2/i18n/keys";
import type { D2Result } from "@d2/result";
import { applyD2Errors } from "$lib/shared/forms/form-helpers.js";

type AnyZodObject = z.ZodObject<Record<string, z.ZodType>>;

interface ValidateAndSubmitOptions<TSchema extends AnyZodObject, TData = void> {
  /** The incoming request event. */
  request: RequestEvent["request"];
  /** The Zod schema to validate against. */
  schema: TSchema;
  /** Called with validated data. Return a D2Result to signal success/failure. */
  submit: (data: z.infer<TSchema>) => Promise<D2Result<TData>>;
  /** Optional success message. */
  successMessage?: string;
}

/**
 * Validate a form submission and call the submit handler.
 *
 * On validation failure: returns Superforms-compatible field errors.
 * On D2Result failure with inputErrors: maps them to Superforms fields.
 * On D2Result failure with messages: returns as a form-level message.
 * On success: returns the form with an optional success message.
 */
export async function validateAndSubmit<TSchema extends AnyZodObject, TData = void>({
  request,
  schema,
  submit,
  successMessage,
}: ValidateAndSubmitOptions<TSchema, TData>) {
  const form = await superValidate(request, zod(schema));

  if (!form.valid) {
    return fail(400, { form });
  }

  const result = await submit(form.data as z.infer<TSchema>);

  if (!result.success) {
    if (result.inputErrors?.length) {
      applyD2Errors(form, [...result.inputErrors]);
      return fail((result.statusCode ?? 400) as NumericRange<400, 599>, { form });
    }

    const errorMessage = result.messages?.join(". ") || TK.common.errors.UNKNOWN;
    return message(form, errorMessage, {
      status: (result.statusCode ?? 500) as NumericRange<400, 599>,
    });
  }

  if (successMessage) {
    return message(form, successMessage);
  }

  return { form };
}
