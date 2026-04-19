import { error } from "@sveltejs/kit";
import { countriesToOptions } from "$lib/shared/forms/geo-ref-data.js";
import { getGeoRefData } from "$lib/server/geo-ref-data.server.js";
import type { PageServerLoad } from "./$types.js";

export const load: PageServerLoad = async () => {
  const refData = await getGeoRefData();
  if (!refData) {
    error(503, "Geo reference data unavailable. Ensure infrastructure services are running.");
  }
  return {
    countries: countriesToOptions(refData.countries ?? {}),
  };
};
