// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

export type { ILogger, LogBindings } from "./i-logger.js";
export { type LoggerOptions, setupLogger } from "./setup-logger.js";
export {
  markRedactedFields,
  getRedactedFieldsFor,
  collectAllRedactedFields,
  clearRedactedFieldsRegistry,
} from "./redaction.js";
export {
  type SanitizedErrorRender,
  sanitizedErrorRender,
} from "./sanitized-error-render.js";
