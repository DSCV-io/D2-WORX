import { superValidate } from "sveltekit-superforms";
import { zod4 as zod } from "sveltekit-superforms/adapters";
import { createForgotPasswordSchema } from "$lib/shared/forms/forgot-password-schema.js";
import type { PageServerLoad } from "./$types.js";

const schema = createForgotPasswordSchema();

export const load: PageServerLoad = async () => {
  const form = await superValidate(zod(schema));
  return { form };
};
