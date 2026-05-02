// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { error } from "@sveltejs/kit";
import { countriesToOptions } from "$lib/shared/forms/geo-ref-data.js";
import { getGeoRefData } from "$lib/server/geo-ref-data.server.js";
import type { PageServerLoad } from "./$types.js";

export const load: PageServerLoad = async () => {
  const refData = await getGeoRefData();
  if (!refData) {
    // TK key — the +error.svelte page passes this through translateMessage().
    error(503, "common_errors_geo_ref_unavailable");
  }
  return {
    countries: countriesToOptions(refData.countries ?? {}),
  };
};
