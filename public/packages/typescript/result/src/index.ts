// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

export { D2Result, type D2ResultInit } from "./d2-result.js";
export { HttpStatusCode } from "./http-status-codes.js";
export {
  ErrorCodes,
  type ErrorCode,
  ALL_ERROR_CODES,
  getErrorHttpStatus,
} from "./error-codes.g.js";
export { type InputError, inputError } from "./input-error.js";
export { InputErrorWireShape } from "./input-error.g.js";
export { type TKMessage, tk } from "@dcsv-io/d2-i18n-abstractions";
export { TkMessageWireShape } from "@dcsv-io/d2-i18n-abstractions";
export {
  D2ResultEnvelopeFieldNames,
  type D2ResultEnvelopeFieldName,
  ALL_D2RESULT_ENVELOPE_FIELD_NAMES,
} from "./d2result-envelope.g.js";
export {
  renderMessage,
  renderMessages,
  renderInputErrors,
  type TranslateFn,
} from "./render-messages.js";
export { bubble, bubbleFail } from "./bubble.js";
export { combine, combineMany } from "./combine.js";
export {
  notFound,
  unauthorized,
  forbidden,
  validationFailed,
  conflict,
  serviceUnavailable,
  unhandledException,
  payloadTooLarge,
  tooManyRequests,
  canceled,
} from "./factories.g.js";
export { ok, created, fail, someFound } from "./factories.js";
