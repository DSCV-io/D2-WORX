import { requireOrg } from "@d2/auth-bff-client";
import type { LayoutServerLoad } from "./$types";

export const load: LayoutServerLoad = async ({ locals, url }) => {
  const { session } = requireOrg(locals, url);

  return {
    orgType: session.activeOrganizationType,
    role: session.activeOrganizationRole,
  };
};
