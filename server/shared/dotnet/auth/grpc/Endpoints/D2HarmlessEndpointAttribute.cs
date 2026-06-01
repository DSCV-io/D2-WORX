// -----------------------------------------------------------------------
// <copyright file="D2HarmlessEndpointAttribute.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Endpoints;

/// <summary>
/// Declares that a gRPC method (or every method on a service class) is a
/// HARMLESS endpoint — the auth interceptor SKIPS the entire JWT validation
/// pipeline (signature + claims + session liveness + scope check) for matching
/// calls. This is a SECURITY-CRITICAL attribute — misuse causes sensitive
/// data to be returned without any authentication.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Legitimate use cases ONLY</strong> — exhaustive enumeration:
/// </para>
/// <list type="bullet">
///   <item>Kubernetes / Docker liveness + readiness probes that return a
///     fixed-shape "healthy / unhealthy" response with NO request-derived data
///     and NO sensitive system state.</item>
///   <item>Intra-cluster service-to-service health or info endpoints that
///     return ONLY closed-enumeration constants (status strings, version
///     identifiers, build metadata) — NEVER user data, organization data,
///     session-derived state, or any field an operator would consider
///     sensitive.</item>
///   <item>OIDC discovery endpoints (Edge service only) — `/.well-known/openid-configuration`
///     and `/.well-known/jwks.json` MUST be reachable pre-auth per OIDC
///     specification. The discovery payload is intentionally public.</item>
/// </list>
/// <para>
/// <strong>If you are tempted to use this attribute on an endpoint that
/// returns user data, organization data, OR ANYTHING THE OPERATOR WOULD
/// CONSIDER SENSITIVE, this attribute is the WRONG TOOL.</strong> Any data
/// exposure beyond the use cases enumerated above is a security bug. Declare
/// an anon-scope-required endpoint instead — that path is the one for
/// "endpoints that should be reachable without an existing user session"
/// (sign-in / password-reset / public lookups). Anon-scope endpoints still
/// flow through the full validator + scope check; <c>[D2HarmlessEndpoint]</c>
/// does NOT.
/// </para>
/// <para>
/// <strong>Why the deliberately odd name</strong>: the surface SKIPS auth
/// entirely. A casual-sounding name like <c>[AllowAnonymous]</c> gets
/// approved on auto-pilot during code review; an odd-sounding name like
/// <c>[D2HarmlessEndpoint]</c> forces the reviewer to pause and ask "is this
/// endpoint actually harmless?" — which is the security-correct framing. The
/// friction is intentional.
/// </para>
/// <para>
/// <strong>Precedence</strong> (matches BCL <c>[AllowAnonymous]</c> over
/// <c>[Authorize]</c>): a method-level <see cref="D2HarmlessEndpointAttribute"/>
/// overrides any class-level <see cref="D2RequireAnyScopeAttribute"/> or
/// <see cref="D2RequireAllScopesAttribute"/> on the same service. Fluent
/// metadata (<c>MethodScopeMetadata.HarmlessEndpoint</c> attached via the
/// builder extensions) takes precedence over all attribute paths.
/// </para>
/// <para>
/// Deliberately NOT named <c>[AllowAnonymous]</c> — the codebase does not
/// recognize the BCL <c>[AllowAnonymous]</c> attribute (its semantic is tied
/// to the BCL <c>AuthorizationMiddleware</c> chain we deliberately bypass).
/// The <c>D2</c> prefix prevents both attribute-name collision AND
/// confusion at the call site.
/// </para>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Class,
    AllowMultiple = false,
    Inherited = false)]
public sealed class D2HarmlessEndpointAttribute : Attribute
{
}
