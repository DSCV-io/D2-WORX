import { superValidate } from "sveltekit-superforms";
import { zod4 as zod } from "sveltekit-superforms/adapters";
import { createResetPasswordSchema } from "$lib/shared/forms/reset-password-schema.js";
import type { PageServerLoad } from "./$types.js";

const schema = createResetPasswordSchema();

export const load: PageServerLoad = async () => {
  const form = await superValidate(zod(schema));
  return { form };
};
