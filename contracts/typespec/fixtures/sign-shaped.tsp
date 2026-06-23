// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Sign-shaped fixture TypeSpec contract.
//
// This fixture exercises every decorator path consumed by the DTO emitter
// and available to future emitters in the fleet:
//   @d2Redact   — payload bytes field → [property: RedactData] in generated C#
//   @d2ServedBy — service ownership annotation
//   @d2InProcess — in-process transport selector (consumed by the route + policy emitters)
//   @d2GrpcMethod — gRPC service/method binding (consumed by the proto + gRPC emitters)
//
// This is NOT KC's real Sign operation. It is a test fixture mirroring the
// shape of a signing operation for the purpose of exercising the emitter fleet.

import "@d2/typespec-decorators";
import "@typespec/http";

using D2;
using Http;

namespace D2.Fixtures;

/** Input for a sign operation: a key identifier and the payload bytes to sign. */
model SignInput {
  /** The key identifier selecting the signing key. */
  @d2Field(1)
  kid: string;

  /** The payload to sign. Redacted in structured logs (secret material). */
  @d2Redact
  @d2Field(2)
  payload: bytes;
}

/** Output of a sign operation: the base64url-encoded signature. */
model SignOutput {
  /** The signature over the payload, base64url-encoded (unpadded). */
  @d2Field(1)
  signature: string;
}

/**
 * Sign the given payload with the specified key.
 * Fixture operation — exercises @d2Redact, @d2ServedBy, @d2InProcess,
 * @d2GrpcMethod, @route, the auth/policy decorators, and the idempotency gate
 * (header keySource, 24-hour TTL) for the emitter fleet.
 */
@d2Command
@d2ServedBy("KeyCustodian")
@d2InProcess
@d2GrpcMethod("KeyCustodianSigner", "Sign")
@route("/internal/v1/kc/sign")
@post
@d2RequireAnyScope("self.write")
@d2RateLimitTier("Standard")
@d2Csrf("exempt")
@d2Idempotent("header", 86400)
op sign(input: SignInput): SignOutput;

/**
 * Sign the given payload with the specified key (derived-key idempotency fixture).
 * Exercises the derived keySource path of the idempotency gate — key is a
 * SHA-256 hash of the `kid` field value, with a 1-hour TTL.
 */
@d2Command
@d2ServedBy("KeyCustodian")
@d2InProcess
@route("/internal/v1/kc/sign-derived")
@post
@d2RequireAnyScope("self.write")
@d2Idempotent("derived", 3600, "kid")
op signDerived(input: SignInput): SignOutput;
