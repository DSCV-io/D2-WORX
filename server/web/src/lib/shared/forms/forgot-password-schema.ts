import { z } from "zod";
import { emailField } from "$lib/shared/forms/schemas.js";

/**
 * Forgot password form schema — email only.
 *
 * BetterAuth returns success regardless of whether the email exists
 * (security best practice), so no async validation needed.
 */
export function createForgotPasswordSchema() {
  return z.object({
    email: emailField(),
  });
}

export type ForgotPasswordFormData = z.infer<ReturnType<typeof createForgotPasswordSchema>>;
