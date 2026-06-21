// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { Country } from "../generated/country.g.js";
import type { Currency } from "../generated/currency.g.js";
import type { GeopoliticalEntity } from "../generated/geopolitical-entity.g.js";
import type { Language } from "../generated/language.g.js";
import type { Locale } from "../generated/locale.g.js";
import type { Subdivision } from "../generated/subdivision.g.js";
import type { Timezone } from "../generated/timezone.g.js";
import type { CountryCode } from "../generated/typed-codes/country-code.g.js";
import type { CurrencyCode } from "../generated/typed-codes/currency-code.g.js";
import type { GeopoliticalEntityCode } from "../generated/typed-codes/geopolitical-entity-code.g.js";
import type { LanguageCode } from "../generated/typed-codes/language-code.g.js";
import type { LocaleCode } from "../generated/typed-codes/locale-code.g.js";
import type { SubdivisionCode } from "../generated/typed-codes/subdivision-code.g.js";
import type { TimezoneCode } from "../generated/typed-codes/timezone-code.g.js";

/**
 * Mirror of .NET `D2.Shared.Geo.Abstractions.IGeoReference` —
 * strongly-typed lookup contract for the seven reference-data catalogs.
 * Every method takes a typed identifier (`*Code` real enum or wrapper
 * struct) and returns the single record shape — the type system enforces
 * the absence of a NotFound branch because the typed identifier IS the
 * catalog.
 *
 * Implementations live in `@d2/geo-default` backed by the codegen-emitted
 * static data; tests can supply ad-hoc fixtures by implementing this
 * interface directly.
 *
 * The lookup methods are exact-match by primary code — they never perform
 * name-based resolution. For free-form text → entity lookups use
 * `IGeoNameResolver` from `../name-resolution/i-geo-name-resolver.js`.
 *
 * Lookups return deprecated entities by default because deprecated codes
 * (`YU`, `SU`, etc.) must remain resolvable for hash citations + audit
 * replay. UI / dropdown code that wants to filter deprecated entries out
 * should walk the catalog arrays and check `entity.deprecation === undefined`
 * explicitly.
 */
export interface IGeoReference {
  /** Look up a `Country` by its typed code. */
  getCountry(code: CountryCode): Country;

  /** Look up a `Subdivision` by its branded ISO 3166-2 code. */
  getSubdivision(code: SubdivisionCode): Subdivision;

  /** Look up a `Timezone` by its branded IANA identifier. */
  getTimezone(code: TimezoneCode): Timezone;

  /** Look up a `Locale` by its branded IETF BCP-47 tag. */
  getLocale(code: LocaleCode): Locale;

  /** Look up a `Currency` by its typed code. */
  getCurrency(code: CurrencyCode): Currency;

  /** Look up a `Language` by its typed code. */
  getLanguage(code: LanguageCode): Language;

  /** Look up a `GeopoliticalEntity` by its typed code. */
  getGeopoliticalEntity(code: GeopoliticalEntityCode): GeopoliticalEntity;
}
