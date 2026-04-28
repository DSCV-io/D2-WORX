import { z } from "zod";
import { emailField } from "$lib/shared/forms/schemas.js";
import * as m from "$lib/paraglide/messages.js";

/**
 * Sign-in form schema — email + password only.
 *
 * No password complexity rules on sign-in — just non-empty.
 * Server handles actual credential validation.
 */
export function createSignInSchema() {
  return z.object({
    email: emailField(),
    password: z.string().min(1, { error: () => m.webclient_forms_required() }),
  });
}

export type SignInFormData = z.infer<ReturnType<typeof createSignInSchema>>;
