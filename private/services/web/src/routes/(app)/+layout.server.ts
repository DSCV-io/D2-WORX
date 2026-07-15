// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { requireOrg } from "$lib/server/auth-bff-stubs";
import type { LayoutServerLoad } from "./$types";

export const load: LayoutServerLoad = async ({ locals, url }) => {
  const { session } = requireOrg(locals, url);

  return {
    orgType: session.activeOrganizationType,
    role: session.activeOrganizationRole,
  };
};
