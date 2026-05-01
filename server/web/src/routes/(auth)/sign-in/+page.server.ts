import { superValidate } from "sveltekit-superforms";
import { zod4 as zod } from "sveltekit-superforms/adapters";
import { createSignInSchema } from "$lib/shared/forms/sign-in-schema.js";
import type { PageServerLoad } from "./$types.js";

const schema = createSignInSchema();

export const load: PageServerLoad = async () => {
  const form = await superValidate(zod(schema));
  return { form };
};
