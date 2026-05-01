import { superValidate, message } from "sveltekit-superforms";
import { zod4 as zod } from "sveltekit-superforms/adapters";
import { fail } from "@sveltejs/kit";
import { createSignUpSchema as createSignupSchema } from "$lib/shared/forms/sign-up-schema.js";
import type { Actions, PageServerLoad } from "./$types.js";

const schema = createSignupSchema();

export const load: PageServerLoad = async () => {
  const form = await superValidate(zod(schema));
  return { form };
};

export const actions: Actions = {
  default: async ({ request }) => {
    const form = await superValidate(request, zod(schema));

    if (!form.valid) {
      return fail(400, { form });
    }

    // Demo: no actual submission — return success message
    return message(form, "Signup form validated successfully!");
  },
};
