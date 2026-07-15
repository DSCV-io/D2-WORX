// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { tsPackagePath } from "../../lib/paths.js";

/**
 * Path helper for the `@dcsv-io/d2-geo-default/src/generated/` directory. Mirrors
 * the `geo-abstractions` GEN_DIR pattern used by the type emitters next door.
 */
export function defaultGenPath(...parts: string[]): string {
  return tsPackagePath("geo", "default", "src", "generated", ...parts);
}
