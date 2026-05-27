// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Barrel re-export — convenience entrypoint for consumers that want every
// catalog at once. Prefer the sub-path imports (e.g.
// `@d2/geo-default/countries`) for bundle-friendliness. Each sub-path
// already triggers the coordinator (wire-nav step) via side-effect import
// on `./generated/geo-data-initializer.g.js`.

// Side-effect import ensures the wire-nav step runs before the barrel
// re-exports resolve. ESM module caching makes this a no-op if any
// sub-path was already imported.
import "./generated/geo-data-initializer.g.js";

export { Countries, CountryLookup } from "./generated/countries.g.js";
export { Subdivisions, SubdivisionLookup } from "./generated/subdivisions.g.js";
export { Currencies, CurrencyLookup } from "./generated/currencies.g.js";
export { Languages, LanguageLookup } from "./generated/languages.g.js";
export { Locales, LocaleLookup } from "./generated/locales.g.js";
export { Timezones, TimezoneLookup } from "./generated/timezones.g.js";
export {
  GeopoliticalEntities,
  GeopoliticalEntityLookup,
} from "./generated/geopolitical-entities.g.js";
export { initializeGeoData } from "./generated/geo-data-initializer.g.js";

// Name resolver — cascade-based free-form text → catalog record matcher.
export {
  DefaultGeoNameResolver,
  tryResolveCountryByName,
  tryResolveSubdivisionByName,
} from "./name-resolution/default-geo-name-resolver.js";

// Default-layer record-returning helpers over IRequestContext geo fields.
export {
  countryFor,
  subdivisionFor,
} from "./extensions/i-request-context-geo-extensions.js";
