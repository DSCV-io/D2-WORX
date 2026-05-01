import { superValidate } from "sveltekit-superforms";
import { zod4 as zod } from "sveltekit-superforms/adapters";
import { createSignUpSchema } from "$lib/shared/forms/sign-up-schema.js";
import type { PageServerLoad } from "./$types.js";

const schema = createSignUpSchema();

export const load: PageServerLoad = async () => {
  const form = await superValidate(zod(schema));
  return { form };
};
