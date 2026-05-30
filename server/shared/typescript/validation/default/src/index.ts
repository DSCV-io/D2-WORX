// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Default validator implementations + the cross-language email pattern.
export {
  DefaultEmailValidator,
  EMAIL_PATTERN,
} from "./default-email-validator.js";
export { DefaultPhoneValidator } from "./default-phone-validator.js";
export { DefaultPostalCodeValidator } from "./default-postal-code-validator.js";

import { DefaultEmailValidator } from "./default-email-validator.js";
import { DefaultPhoneValidator } from "./default-phone-validator.js";
import { DefaultPostalCodeValidator } from "./default-postal-code-validator.js";

/**
 * Ready-to-use singleton validators. The default implementations are
 * stateless, so a shared instance per process is safe and avoids
 * re-allocating the compiled email regex on every call site.
 */
export const emailValidator = new DefaultEmailValidator();
export const phoneValidator = new DefaultPhoneValidator();
export const postalCodeValidator = new DefaultPostalCodeValidator();
