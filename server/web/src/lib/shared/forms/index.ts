// Form utilities barrel export
export {
  nameField,
  emailField,
  phoneField,
  phoneFieldOptional,
  postcodeField,
  streetField,
  urlField,
  currencyField,
} from "./schemas.js";

export { mapD2Errors, applyD2Errors } from "./form-helpers.js";

export { digitsOnly, alphaOnly, noSpaces, uppercase, maxLength, compose } from "./input-filters.js";

export {
  formatPhoneDisplay,
  formatPhoneAsYouType,
  getCountryCallingCode,
  parsePhone,
} from "./phone-format.js";

export {
  countriesToOptions,
  subdivisionsForCountry,
  buildCountriesWithSubdivisions,
  type CountryOption,
  type SubdivisionOption,
} from "./geo-ref-data.js";
