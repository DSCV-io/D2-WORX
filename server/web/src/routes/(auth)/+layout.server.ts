import { redirectIfAuthenticated } from "@d2/auth-bff-client";
import type { LayoutServerLoad } from "./$types";

export const load: LayoutServerLoad = async ({ locals }) => {
  redirectIfAuthenticated(locals);
  return {};
};
