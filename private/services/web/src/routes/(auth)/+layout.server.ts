// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { redirectIfAuthenticated } from "$lib/server/auth-bff-stubs";
import type { LayoutServerLoad } from "./$types";

export const load: LayoutServerLoad = async ({ locals }) => {
  redirectIfAuthenticated(locals);
  return {};
};
