// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { createTypeSpecLibrary } from "@typespec/compiler";

// Library descriptor for the @d2/typespec-decorators package.
// Diagnostics are empty — the typed diagnostic catalog
// (bad scope/audience/tier/target) is added with the validation layer.
export const $lib = createTypeSpecLibrary({
  name: "@d2/typespec-decorators",
  diagnostics: {},
});
