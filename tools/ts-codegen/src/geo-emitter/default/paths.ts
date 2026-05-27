// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { tsPackagePath } from "../../lib/paths.js";

/**
 * Path helper for the `@d2/geo-default/src/generated/` directory. Mirrors
 * the `geo-abstractions` GEN_DIR pattern used by the type emitters next door.
 */
export function defaultGenPath(...parts: string[]): string {
  return tsPackagePath("geo-default", "src", "generated", ...parts);
}
