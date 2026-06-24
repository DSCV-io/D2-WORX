// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// KeyCustodian TypeSpec contract.
//
// Defines the GetJwks operation and its supporting Jwk model, mirroring
// the KC live handler's wire-relevant public surface.
//
// Transport-vs-domain-VO divergence note:
//   The live domain `Jwk` value object has 3 positional ctor params
//   (Kid, N, E) + 3 init-only properties with constant defaults (Kty="RSA",
//   Use="sig", Alg="RS256"). TypeSpec models have no concept of
//   constant-defaulted init-only properties, so the generated transport DTO
//   expresses all 6 as regular positional fields. The generated `Jwk` is a
//   TRANSPORT DTO, structurally equivalent by public member set (same 6
//   properties, same names, same types), but not constructor-identical to the
//   domain VO. This is intentional and expected — the generated DTO does not
//   replace the domain VO.

import "@d2/typespec-decorators";
import "@typespec/versioning";

using D2;
using TypeSpec.Versioning;

@versioned(D2.KeyCustodian.Versions)
namespace D2.KeyCustodian {
  /** Stability-channel versions for the D2.KeyCustodian wire surface. */
  enum Versions {
    v2alpha: "v2alpha",
  }

  /** A single RFC 7517 JSON Web Key for an RS256 public signing key. */
  model Jwk {
    /** The key identifier matching the JWT kid header. */
    kid: string;

    /** The RSA modulus, base64url-encoded (unpadded). */
    n: string;

    /** The RSA public exponent, base64url-encoded (unpadded). */
    e: string;

    /** The key type — always RSA for the signing domain. */
    kty: string;

    /** The intended key use — always sig (signature verification). */
    use: string;

    /** The signing algorithm — always RS256. */
    alg: string;
  }

  /** RFC 7517 JWKS document: the currently-serving public signing keys. */
  model GetJwksOutput {
    /** The JWK entries, active key(s) first. */
    keys: Jwk[];
  }

  /**
   * Return the JWKS document for the signing domain.
   * The JWKS document is computed entirely from the signing keys in the store,
   * so the query takes no parameters.
   */
  @d2Query
  @d2InProcess
  @d2ServedBy("KeyCustodian")
  op getJwks(): GetJwksOutput;
}
