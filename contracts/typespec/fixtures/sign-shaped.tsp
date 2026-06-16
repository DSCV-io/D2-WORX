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

using D2;

namespace D2.Fixtures;

/** Input for a sign operation: a key identifier and the payload bytes to sign. */
model SignInput {
  /** The key identifier selecting the signing key. */
  kid: string;

  /** The payload to sign. Redacted in structured logs (secret material). */
  @d2Redact
  payload: bytes;
}

/** Output of a sign operation: the base64url-encoded signature. */
model SignOutput {
  /** The signature over the payload, base64url-encoded (unpadded). */
  signature: string;
}

/**
 * Sign the given payload with the specified key.
 * Fixture operation — exercises @d2Redact, @d2ServedBy, @d2InProcess,
 * and @d2GrpcMethod for the emitter fleet.
 */
@d2Command
@d2ServedBy("KeyCustodian")
@d2InProcess
@d2GrpcMethod("KeyCustodianSigner", "Sign")
op sign(input: SignInput): SignOutput;
