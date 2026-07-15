// ============================================================================
// D2-WORX TypeSpec spike — KeyCustodian `Sign` operation
//
// DESIGN SKETCH ONLY. This file does not need to compile against a real
// TypeSpec installation. It demonstrates:
//   (a) the custom D2 decorator vocabulary
//   (b) dual REST+gRPC single-source binding for one op
//   (c) auth-then-forward declaration (scope + @d2ServedBy)
//   (d) the in-process leaf owner annotation
//
// Sections:
//   1. Decorator declarations (would live in @d2/typespec-decorators package)
//   2. Model definitions (SignInput, SignOutput)
//   3. The Sign operation — dual-bound, scoped, owner-declared
//   4. Companion gRPC service declaration (the gRPC surface of the same op)
// ============================================================================

import "@typespec/http";           // provides @route, @post, @header, @body, etc.
import "@typespec/openapi3";       // provides @info, @extension for OpenAPI emit

using TypeSpec.Http;

// ============================================================================
// 1. D2 decorator declarations
//    In the real package these live in a separate .tsp file that is imported.
//    They are inlined here for the spike's self-containedness.
//    Each decorator stores its argument in a StateMap keyed by a symbol so
//    emitters can read it via program.stateMap(SYMBOL).get(op).
// ============================================================================

/**
 * Declares the required OAuth 2.0 scope for an operation. The value must
 * exist in contracts/auth-scopes/scopes.spec.json; the parity emitter
 * generates a test that asserts this at CI time.
 *
 * Example: @d2Scope("internal.kc.sign")
 */
extern dec d2Scope(target: Operation, scope: string);

/**
 * Declares the rate-limit tier for a REST-bound operation. Known tiers:
 *   "Internal"    — not externally reachable; no rate-limit bucket applies
 *   "Restricted"  — high-sensitivity, low-cap bucket
 *   "Standard"    — default public-API bucket
 *   "Elevated"    — burst-tolerant authenticated bucket
 */
extern dec d2RateLimitTier(target: Operation, tier: string);

/**
 * Declares the JWT audience claim expected on tokens presented to this
 * operation. The value must exist in contracts/auth-audiences/audiences.spec.json.
 * For in-Edge internal ops the audience is always "d2-edge".
 */
extern dec d2Audience(target: Operation, audience: string);

/**
 * Names the module/service that OWNS this operation's handler body.
 * The generated Edge forwarding layer uses this to route to either:
 *   - the in-process leaf (IKeyCustodianInternalApi) when the owner is an
 *     Edge module, OR
 *   - a generated gRPC client when the owner is an external service.
 *
 * Example: @d2ServedBy("KeyCustodian")
 *
 * The emitter resolves "KeyCustodian" against the known Edge-module registry
 * (hard-coded in the emitter config for the spike; driven by a modules.spec.json
 * in the real pipeline). In-Edge → leaf; external → gRPC forward stub.
 */
extern dec d2ServedBy(target: Operation, owner: string);

/**
 * Dual-binding marker: declares that this operation ALSO binds to a gRPC
 * method in addition to its HTTP binding. The emitter reads this to emit
 * the proto service + method alongside the REST route registration.
 *
 * Parameters:
 *   service  — the proto service name (e.g. "KeyCustodianInternal")
 *   method   — the proto method name (e.g. "Sign")
 *
 * DUAL-BINDING CONVENTION NOTE (the kill-switch surface):
 * TypeSpec's @typespec/http library walks the HTTP decorator state to build
 * the REST binding; our custom @d2GrpcMethod stores its state in a separate
 * StateMap symbol. The proto emitter reads BOTH: the operation's model fields
 * to build the proto message, and the @d2GrpcMethod state to name the service
 * + method. Critically, these two emitters SHARE the same TypeSpec model —
 * they diverge only in their encoding (JSON ↔ protobuf). If TypeSpec's HTTP
 * model ever *flattens* path/query params into the op signature in a way that
 * makes the proto message unrecoverable, the kill-switch fires: we express the
 * gRPC binding as a separate companion interface (see §EMITTERS.md §4).
 */
extern dec d2GrpcMethod(target: Operation, service: string, method: string);

/**
 * Marks a model field as containing PII that must be redacted in structured
 * logs. Emitted as [RedactData] on the C# record field and as a Zod .redact()
 * call on the TS DTO. Mirrors the existing [RedactData] attribute usage.
 */
extern dec d2Redact(target: ModelProperty);

// ============================================================================
// 2. Model definitions
// ============================================================================

/**
 * Input to the Sign operation.
 *
 * The caller (Auth module) provides:
 *   - keyDomain: the logical key domain whose active RSA key should sign
 *     (e.g. "jwt-signing"). Validated against KeyDomain.TryCreate in the
 *     generated I<Op>Handler input.
 *   - payload: the raw bytes to sign. For JWT issuance this is the
 *     base64url(header).base64url(claims) octets. The handler does NOT
 *     interpret the payload — it signs and returns the compact signature.
 *
 * Wire format:
 *   REST  → JSON body: { "keyDomain": "jwt-signing", "payload": "<base64url>" }
 *   gRPC  → proto message SignRequest { string key_domain = 1; bytes payload = 2; }
 *
 * The dual-binding convention: `payload` is bytes in proto and string (base64url)
 * in JSON. The binding-mapper convention resolves this: the HTTP emitter emits a
 * string field annotated @format("byte"); the proto emitter emits a bytes field.
 * The Edge transcoding layer uses the generated binding-map (see EMITTERS.md §5)
 * to convert between the two representations at the REST↔gRPC boundary.
 */
model SignInput {
  /** Logical key domain whose active RSA signing key to use. */
  keyDomain: string;

  /**
   * Raw octets to sign (base64url-encoded in JSON wire; bytes in proto).
   * @d2Redact — the payload is caller-supplied content, potentially PII
   * (it is the JWT claims block before signing).
   */
  @d2Redact
  payload: bytes;
}

/**
 * Output from the Sign operation.
 *
 * Wire format:
 *   REST  → JSON body: { "signature": "<base64url>", "kid": "<kid>" }
 *   gRPC  → proto message SignResponse { string signature = 1; string kid = 2; }
 */
model SignOutput {
  /**
   * The RSA-PKCS1v15 / RS256 signature over the payload, base64url-encoded.
   * The caller assembles the compact JWT as:
   *   base64url(header) + "." + base64url(claims) + "." + signature
   */
  signature: string;

  /**
   * The kid of the active key that produced this signature. The caller places
   * this in the JWT header { "alg": "RS256", "kid": "<this value>" }.
   */
  kid: string;
}

// ============================================================================
// 3. The Sign operation — dual REST+gRPC binding, scoped, owner-declared
//
// This is the single source of truth for the Sign operation. Every emitter
// reads from this one definition. The route, the gRPC method, the C# DTO,
// the TS DTO, the proto message, the scope enforcement, and the leaf
// interface — all generated from what you see here.
// ============================================================================

/**
 * KeyCustodian internal API — operations that are called by in-Edge peers
 * (Auth module) or, in the future, by backend services via gRPC.
 *
 * The @route here is the Edge-side REST surface for in-process callers that
 * happen to go through the HTTP pipeline (e.g. tests, future BFF callers).
 * In production, Auth calls the leaf directly (IKeyCustodianInternalApi);
 * the REST route is the fallback + the external gRPC caller's entry point.
 */
@route("/internal/v1/kc")
interface KeyCustodianInternalOps {
  /**
   * Sign a payload with KeyCustodian's active RSA signing key for the
   * specified key domain.
   *
   * Auth caller flow:
   *   Auth.App.Tokens.IssueToken handler
   *     → IKeyCustodianInternalApi.Sign(input)   [leaf, in-process, no HTTP]
   *     ← SignOutput { signature, kid }
   *     → assembles compact JWT
   *
   * External gRPC caller flow (future backend service):
   *   Generated gRPC client stub
   *     → KeyCustodianInternal.Sign(SignRequest)
   *     ← SignResponse
   *
   * REST route (internal only — not reachable from the public internet;
   * enforced at the Edge's routing middleware by the "Internal" tier):
   *   POST /internal/v1/kc/sign
   *   Authorization: Bearer <internal-service-token>
   *   Scope required: internal.kc.sign
   *
   * Dual-binding declaration:
   *   @d2GrpcMethod("KeyCustodianInternal", "Sign") places the gRPC
   *   service+method name in the proto emitter's StateMap. The proto
   *   emitter reads SignInput/SignOutput and emits:
   *     service KeyCustodianInternal { rpc Sign(SignRequest) returns (SignResponse); }
   *     message SignRequest { string key_domain = 1; bytes payload = 2; }
   *     message SignResponse { string signature = 1; string kid = 2; }
   *
   * Owner declaration:
   *   @d2ServedBy("KeyCustodian") tells the forwarding emitter that the
   *   Edge REST route forwards to IKeyCustodianInternalApi (leaf, in-process)
   *   rather than a gRPC client. No hand-written passthrough code is needed.
   */
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
// 4. D2ErrorResponse model (reusable across all ops)
//
// Represents the wrapped-result wire model (ADR-0019 / d2result-envelope spec).
// In the real emitter this is imported from a shared TypeSpec library, not
// re-declared per op-file. Inlined here for spike self-containedness.
// ============================================================================

/**
 * The D2 wrapped error response — emitted as the non-2xx arm of every op's
 * REST union return. Mirrors d2result-envelope.spec.json on the wire.
 *
 * The C# emitter generates: sealed record D2ErrorResponse(string Code,
 *   string Category, string UserMessageKey, ...) — it does NOT hand-write
 * this (it's generated from the d2result-envelope spec, not from here).
 * The TypeSpec model here is the IDL-level declaration that binds the
 * HTTP error responses to the existing generated envelope shape.
 */
@error
model D2ErrorResponse {
  @statusCode _: int32;
  code: string;
  category: string;
  userMessageKey: string;
  traceId?: string;
  correlationId?: string;
}

// ============================================================================
// 5. Leaf interface declaration (informational — not TypeSpec-compilable today)
//
// This block shows WHAT the leaf emitter generates. In the real pipeline the
// C# emitter reads the @d2ServedBy decorator and emits:
//
//   // GENERATED — do not edit
//   // Source: contracts/keycustodian-ops/sign.tsp
//   namespace D2.Edge.KeyCustodian.App;
//
//   /// <summary>
//   /// In-process leaf interface for KeyCustodian operations consumed by
//   /// in-Edge peers (Auth module). Generated from the op-IDL.
//   /// </summary>
//   public interface IKeyCustodianInternalApi
//   {
//       Task<D2Result<SignOutput>> SignAsync(SignInput input,
//           CancellationToken ct = default);
//   }
//
// The Auth module's handler receives IKeyCustodianInternalApi via DI
// (registered in D2.Edge.KeyCustodian.App's AddD2KeyCustodianApp()).
// The implementation is the hand-written SignHandler bound to the contract.
//
// The forwarding emitter generates the Edge route glue:
//
//   // GENERATED — do not edit
//   app.MapPost("/internal/v1/kc/sign", async (
//       [FromBody] SignInput input,
//       IKeyCustodianInternalApi kc,
//       CancellationToken ct) =>
//   {
//       var result = await kc.SignAsync(input, ct);
//       return result.ToHttpResult();  // D2Result → IResult mapper
//   })
//   .RequireAuthorization(policy => policy
//       .RequireAuthenticatedUser()
//       .RequireClaim("scope", "internal.kc.sign")
//       .RequireAudience("d2-edge"));
//
// ============================================================================

// ============================================================================
// 6. Proto output (informational — what the proto emitter generates)
//
//   syntax = "proto3";
//   package d2.keycustodian.internal.v1;
//   option csharp_namespace = "D2.Edge.KeyCustodian.Grpc.Internal.V1";
//
//   service KeyCustodianInternal {
//     rpc Sign(SignRequest) returns (SignResponse);
//   }
//
//   message SignRequest {
//     string key_domain = 1;
//     bytes  payload    = 2;
//   }
//
//   message SignResponse {
//     string signature = 1;
//     string kid       = 2;
//   }
//
// This proto is fed to buf + Grpc.Tools for the gRPC wire stub generation.
// It is NOT hand-authored — the proto emitter produces it from prototype.tsp.
// ============================================================================
