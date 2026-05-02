// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { redirect } from "@sveltejs/kit";

export function load() {
  redirect(302, "/debug/design/account-components/profile");
}
