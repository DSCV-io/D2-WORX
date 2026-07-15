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

import "@dcsv-io/d2-typespec-decorators";
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
  @d2Concern("Jwks")
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
  @d2Concern("OidcConfiguration")
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
  @d2Concern("Signing")
  @d2InProcess
  @d2GrpcMethod("KeyCustodianSigner", "Sign")
  @d2RequireAnyScope("internal.kc.sign")
  @d2Audience("d2.internal")
  op sign(input: SignInput): SignOutput;

  /** Input to fetch a payload keyring: the payload-encryption key domain whose keyring to assemble. */
  model GetKeyringInput {
    /** The payload-encryption key domain whose Active + Retiring keyring to fetch. */
    @d2Field(1)
    keyDomain: string;
  }

  /** A single decryptable key in a payload keyring: its kid plus the raw AES key bytes. */
  model KeyringEntry {
    /** The kid identifying this key (matches an encryption-frame recipient kid). */
    @d2Field(1)
    kid: string;

    /**
     * The raw AES-256 key bytes. Custody transfers to the caller (which copies them
     * into a PayloadCryptoKeyring). Secret material — redacted in logs.
     */
    @d2Redact("SecretInformation")
    @d2Field(2)
    keyBytes: bytes;
  }

  /** Output of getKeyring: the active kid, every decryptable entry, and the AAD context. */
  model GetKeyringOutput {
    /** The kid new encryptions must use (the single Active key). */
    @d2Field(1)
    activeKid: string;

    /** Every decryptable key (Active + Retiring), active first then retiring newest-activated-first. */
    @d2Field(2)
    entries: KeyringEntry[];

    /**
     * The AEAD additional-authenticated-data bound to every (en|de)crypt operation for
     * this domain (UTF-8 "d2/<domain>"). Authenticated, not secret (GCM AAD is public
     * context by construction) — deliberately NOT redacted, so operators can debug a
     * cross-service AAD disagreement. Stable-per-domain-for-life: changing it breaks
     * decryption of everything already encrypted under the domain.
     */
    @d2Field(3)
    aadContext: bytes;
  }

  /**
   * Fetch the payload-encryption keyring for a key domain: the active kid, every
   * decryptable (Active + Retiring) AES key, and the domain's AAD context. Read-only
   * Query (no persistent mutation). In-process + gRPC only — never a public REST surface
   * (keyring distribution is internal service-to-service / in-process). Custody of the
   * raw key bytes transfers to the caller, which assembles a PayloadCryptoKeyring; the
   * aadContext is stable-per-domain-for-life. A non-payload key domain is denied by the
   * keyring authority arm (403); a payload domain with no active key yet returns the
   * retryable 503.
   */
  @d2Query
  @d2ServedBy("KeyCustodian")
  @d2Concern("Keyring")
  @d2InProcess
  @d2GrpcMethod("KeyCustodianKeyring", "GetKeyring")
  @d2RequireAnyScope("internal.kc.keyring")
  @d2Audience("d2.internal")
  op getKeyring(input: GetKeyringInput): GetKeyringOutput;

  /** Input to issueLeaf: the workload's PKCS#10 certificate-signing request. */
  model IssueLeafInput {
    /**
     * The DER-encoded PKCS#10 certificate-signing request. PUBLIC material — a CSR
     * carries the workload's public key, request metadata, and a self-signature
     * proving possession of the corresponding private key; it never carries the
     * private key. Deliberately NOT redacted. KeyCustodian verifies the proof-of-
     * possession and the P-256 key policy, IGNORES the CSR's subject/SAN, and signs
     * the extracted public key against the authenticated mTLS peer identity.
     */
    @d2Field(1)
    csrDer: bytes;
  }

  /** Output of issueLeaf: the signed leaf + its issuing intermediate + validity window. */
  model IssueLeafOutput {
    /** The DER-encoded signed leaf certificate. PUBLIC — presented on the wire in the TLS handshake. */
    @d2Field(1)
    certificateDer: bytes;

    /** The DER-encoded issuing-intermediate certificate so the workload can present the full chain. PUBLIC. */
    @d2Field(2)
    issuerCertificateDer: bytes;

    /** The leaf's not-before instant (ISO-8601). */
    @d2Field(3)
    notBefore: utcDateTime;

    /** The leaf's not-after instant (ISO-8601) — drives the refresh-ahead reissue condition. */
    @d2Field(4)
    notAfter: utcDateTime;
  }

  /** Input to getCaCertificate: none — the CA chain is selected entirely from the active managed keys. */
  model GetCaCertificateInput {}

  /** Output of getCaCertificate: the full CA chain (root + issuing intermediate). */
  model GetCaCertificateOutput {
    /**
     * The DER-encoded root CA certificate — the trust anchor a workload pins. PUBLIC
     * trust material presented on the wire in every TLS handshake; deliberately NOT
     * redacted so operators can debug a chain disagreement, exactly like the CA cert
     * material's not-redacted posture.
     */
    @d2Field(1)
    rootCertificateDer: bytes;

    /** The DER-encoded issuing-intermediate CA certificate — chain convenience. PUBLIC. */
    @d2Field(2)
    intermediateCertificateDer: bytes;
  }

  /**
   * Issue a short-lived workload leaf certificate from a PKCS#10 certificate-signing
   * request. Cross-process only (the hard gate) — a non-cross-process origin can never
   * authorize minting a workload identity. The leaf subject-alternative-name is ALWAYS
   * the authenticated mTLS peer identity (structural self-issue); the CSR's subject/SAN
   * is ignored by construction. KeyCustodian verifies the CSR's proof-of-possession,
   * enforces the ECDSA P-256 key policy, and signs the extracted public key — it never
   * sees or returns the leaf private key. Output = leaf + issuing intermediate (the
   * trust anchor distributes via getCaCertificate, never on issuance). All wire material
   * is public — no redaction anywhere on this op.
   */
  @d2Command
  @d2ServedBy("KeyCustodian")
  @d2Concern("Issuance")
  @d2InProcess
  @d2GrpcMethod("KeyCustodianCertificateAuthority", "IssueWorkloadCertificate")
  @d2RequireAnyScope("internal.kc.issue")
  @d2Audience("d2.internal")
  op issueLeaf(input: IssueLeafInput): IssueLeafOutput;

  /**
   * Fetch the certificate-authority chain (root + issuing intermediate). Read-only Query
   * (no persistent mutation). In-process + gRPC only — the trust anchor is distributed
   * over already-trusted internal channels, never the public plane. The material is public
   * trust anchor / chain material (presented on the wire in every TLS handshake) so the
   * authority is broad within the served planes (no per-workload policy map). A missing
   * active root or intermediate returns the retryable 503.
   */
  @d2Query
  @d2ServedBy("KeyCustodian")
  @d2Concern("CaCertificate")
  @d2InProcess
  @d2GrpcMethod("KeyCustodianCaCertificate", "GetCaCertificate")
  @d2RequireAnyScope("internal.kc.cacert")
  @d2Audience("d2.internal")
  op getCaCertificate(input: GetCaCertificateInput): GetCaCertificateOutput;

  /** Input to getOrLazyProvisionSealPublicKey: the target service whose PUBLIC sealing key to fetch. */
  model GetOrLazyProvisionSealPublicKeyInput {
    /** The service id whose seal:<serviceId> public sealing key to fetch (to seal a payload TO it). */
    @d2Field(1)
    serviceId: string;
  }

  /** A single fetchable PUBLIC sealing key: its kid plus the SPKI public key bytes. */
  model SealPublicEntry {
    /** The kid identifying this sealing key (matches a sealed-frame recipient kid). */
    @d2Field(1)
    kid: string;

    /**
     * The SPKI-encoded P-256 ECDH public key bytes. PUBLIC material — a producer seals TO
     * it; harmless to over-share. Deliberately NOT redacted, like the JWKS signing key's
     * public half.
     */
    @d2Field(2)
    publicSpki: bytes;
  }

  /** Output of getOrLazyProvisionSealPublicKey: the active kid + every fetchable public sealing key (Active + Retiring). */
  model GetOrLazyProvisionSealPublicKeyOutput {
    /** The kid new seals must use (the single Active sealing key). */
    @d2Field(1)
    activeKid: string;

    /** Every public sealing key (Active + Retiring), active first then retiring newest-activated-first. */
    @d2Field(2)
    entries: SealPublicEntry[];
  }

  /** A single fetchable PRIVATE sealing key: its kid plus the PKCS#8 private key bytes. */
  model SealPrivateEntry {
    /** The kid identifying this sealing key (matches a sealed-frame recipient kid). */
    @d2Field(1)
    kid: string;

    /**
     * The PKCS#8-encoded P-256 ECDH private key bytes. Custody transfers to the caller
     * (which assembles a RecipientPrivateKeyring to open frames sealed to it). Secret
     * material — redacted in logs.
     */
    @d2Redact("SecretInformation")
    @d2Field(2)
    privatePkcs8: bytes;
  }

  /** Output of getOrLazyProvisionOwnSealPrivateKey: the active kid + every openable private sealing key (Active + Retiring). */
  model GetOrLazyProvisionOwnSealPrivateKeyOutput {
    /** The kid new seals to this service use (the single Active sealing key). */
    @d2Field(1)
    activeKid: string;

    /** Every private sealing key (Active + Retiring), active first then retiring newest-activated-first. */
    @d2Field(2)
    entries: SealPrivateEntry[];
  }

  /**
   * Fetch a target service's PUBLIC sealing key (its active + retiring SPKI public keys)
   * to seal a payload TO it. Any authenticated workload on a served plane (cross-process
   * or in-process) may fetch ANY service's public key — public material is harmless to
   * over-share; the transport scope gates whether the caller may seal at all. A @d2Command
   * because the FIRST request for a service's seal domain provisions its keypair lazily (a
   * persistent write). The served material is public — no redaction. A no-active-key window
   * (a lost provisioning race not yet visible) returns the retryable 503.
   */
  @d2Command
  @d2ServedBy("KeyCustodian")
  @d2Concern("Sealing")
  @d2InProcess
  @d2GrpcMethod("KeyCustodianSealPublicKey", "GetOrLazyProvisionSealPublicKey")
  @d2RequireAnyScope("internal.kc.seal.encrypt")
  @d2Audience("d2.internal")
  op getOrLazyProvisionSealPublicKey(input: GetOrLazyProvisionSealPublicKeyInput): GetOrLazyProvisionSealPublicKeyOutput;

  /**
   * Fetch the caller's OWN PRIVATE sealing key (its active + retiring PKCS#8 private keys)
   * to open frames sealed to it. Cross-process only (the seal-decrypt hard gate) — a caller can
   * only ever obtain ITS OWN key because the op carries NO target: the key is selected purely
   * from the authenticated mTLS peer identity, which is unforgeable only on the cross-process
   * plane. Impersonation is structurally unrepresentable. A @d2Command because the first
   * request provisions the service's keypair lazily. The private key bytes are redacted in
   * logs; a no-active-key window returns the retryable 503.
   */
  @d2Command
  @d2ServedBy("KeyCustodian")
  @d2Concern("Sealing")
  @d2InProcess
  @d2GrpcMethod("KeyCustodianOwnSealPrivateKey", "GetOrLazyProvisionOwnSealPrivateKey")
  @d2RequireAnyScope("internal.kc.seal.open")
  @d2Audience("d2.internal")
  op getOrLazyProvisionOwnSealPrivateKey(): GetOrLazyProvisionOwnSealPrivateKeyOutput;
}
