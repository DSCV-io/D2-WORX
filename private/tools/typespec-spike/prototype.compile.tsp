// ============================================================================
// D2-WORX TypeSpec spike — COMPILE-READY prototype (path (a)).
//
// Identical in shape to prototype.tsp, but the inline `extern dec` block is
// replaced by an import of the real decorator library (@dcsv-io/d2-typespec-decorators),
// so the FULL prototype compiles WITH the @d2* decorators applied and their
// values land in the program StateMap for the emitter to read back.
// ============================================================================

import "@typespec/http";
import "@typespec/openapi3";
import "@dcsv-io/d2-typespec-decorators";

using TypeSpec.Http;
using D2;

// ============================================================================
// Models
// ============================================================================

model SignInput {
  /** Logical key domain whose active RSA signing key to use. */
  keyDomain: string;

  /** Raw octets to sign (base64url-encoded in JSON wire; bytes in proto). */
  @d2Redact
  payload: bytes;
}

model SignOutput {
  /** RS256 signature over the payload, base64url-encoded. */
  signature: string;

  /** kid of the active key that produced this signature. */
  kid: string;
}

// ============================================================================
// The Sign operation — dual REST+gRPC binding, scoped, owner-declared
// ============================================================================

@route("/internal/v1/kc")
interface KeyCustodianInternalOps {
  @post
  @route("/sign")
  @d2Scope("internal.kc.sign")
  @d2Audience("d2-edge")
  @d2RateLimitTier("Internal")
  @d2ServedBy("KeyCustodian")
  @d2GrpcMethod("KeyCustodianInternal", "Sign")
  sign(@body input: SignInput): SignOutput | D2ErrorResponse;
}

// ============================================================================
// D2ErrorResponse — the non-2xx arm of every op's REST union return
// ============================================================================

@error
model D2ErrorResponse {
  // The wrapped-result envelope's HTTP status. Modeled as the concrete set of
  // error codes the d2result-envelope spec produces (ErrorCategory -> httpStatus).
  // A bare `int32` is rejected by @typespec/http (status-code-invalid); the real
  // emitter binds this to the generated category->status map.
  @statusCode statusCode: 400 | 401 | 403 | 404 | 409 | 413 | 422 | 500 | 503;
  code: string;
  category: string;
  userMessageKey: string;
  traceId?: string;
  correlationId?: string;
}
