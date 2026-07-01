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
import "@typespec/http";
import "@typespec/versioning";

using D2;
using Http;
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
   * so the query takes no parameters. Served at the OIDC-canonical
   * /.well-known/jwks.json so any HTTP JWKS client can fetch the public keys;
   * the JWKS is public by definition (@d2Harmless — auth-exempt). The
   * @d2InProcess leaf entry stays for the in-host auth module.
   */
  @d2Query
  @d2InProcess
  @d2ServedBy("KeyCustodian")
  @route("/.well-known/jwks.json")
  @get
  @d2Harmless
  op getJwks(): GetJwksOutput;

  /** Minimal OIDC discovery document (issuer + jwks_uri + RS256 signing algs). */
  model GetOidcConfigurationOutput {
    /** The token issuer identifier (the Edge external base URL). */
    issuer: string;

    /** Absolute URL of the JWKS document. */
    @encodedName("application/json", "jwks_uri")
    jwksUri: string;

    /** Supported JWT signing algorithms — always ["RS256"]. */
    @encodedName("application/json", "id_token_signing_alg_values_supported")
    idTokenSigningAlgValuesSupported: string[];

    /** Supported response types — minimal set for the discovery handshake. */
    @encodedName("application/json", "response_types_supported")
    responseTypesSupported: string[];

    /** Supported subject identifier types. */
    @encodedName("application/json", "subject_types_supported")
    subjectTypesSupported: string[];
  }

  /**
   * Serve the minimal OIDC discovery document so OIDC/JWKS clients auto-discover
   * jwks_uri. Config-reading Query; the token_endpoint / grant-type fields belong
   * to the not-yet-built token endpoint and are added later. Public discovery
   * surface (@d2Harmless — auth-exempt).
   */
  @d2Query
  @d2InProcess
  @d2ServedBy("KeyCustodian")
  @route("/.well-known/openid-configuration")
  @get
  @d2Harmless
  op getOidcConfiguration(): GetOidcConfigurationOutput;

  /** Input to sign: the key domain to sign with, and the bytes to sign. */
  model SignInput {
    /** The signing key domain. Cross-process callers may NOT request "jwks-signing". */
    @d2Field(1)
    keyDomain: string;

    /** The exact bytes to sign (e.g. the JWT signing-input). Secret-adjacent — redacted in logs. */
    @d2Redact("SecretInformation")
    @d2Field(2)
    signingInput: bytes;
  }

  /** Output of sign: the signature + the kid that produced it (so the verifier picks the right JWK). */
  model SignOutput {
    /** The signature over signingInput, base64url-encoded (unpadded). */
    @d2Field(1)
    signature: string;

    /** The kid of the active signing key used (matches a JWKS entry). */
    @d2Field(2)
    kid: string;
  }

  /**
   * Sign a payload with a KeyCustodian-managed signing key. Read-only Query (no
   * persistent mutation). In-process + gRPC only — never a public REST surface
   * (signing is internal service-to-service / in-process minter). The general sign
   * surface signs every domain EXCEPT the cluster-signing root "jwks-signing" (which
   * is reachable only through the dedicated minter capability); a cross-process caller
   * signs per its allowed-signing-domains policy.
   */
  @d2Query
  @d2ServedBy("KeyCustodian")
  @d2InProcess
  @d2GrpcMethod("KeyCustodianSigner", "Sign")
  @d2RequireAnyScope("internal.kc.sign")
  @d2Audience("d2.internal")
  op sign(input: SignInput): SignOutput;
}
