// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Side-effect import — guarantees the GeoDataInitializer coordinator
// runs (wire-nav step) before consumer code touches the catalog data.
import "./generated/geo-data-initializer.g.js";

export { Countries, CountryLookup } from "./generated/countries.g.js";
