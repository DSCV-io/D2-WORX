// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { requireAuth } from "$lib/server/auth-bff-stubs";
import type { LayoutServerLoad } from "./$types";

export const load: LayoutServerLoad = async ({ locals, url }) => {
  const { user } = requireAuth(locals, url);
  return { user };
};
