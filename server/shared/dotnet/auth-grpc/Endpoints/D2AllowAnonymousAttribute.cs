// -----------------------------------------------------------------------
// <copyright file="D2AllowAnonymousAttribute.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Endpoints;

/// <summary>
/// Declares that a gRPC method (or every method on a service class) accepts
/// anonymous requests — the auth interceptor short-circuits the validator +
/// liveness pipeline. Required for the codegen anonymous family (sign-in,
/// password reset, public lookups, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately NOT named <c>[AllowAnonymous]</c> — the codebase does not
/// recognize the BCL <c>[AllowAnonymous]</c> attribute (its semantic is tied
/// to the BCL <c>AuthorizationMiddleware</c> chain we deliberately bypass).
/// The <c>D2</c> prefix prevents both attribute-name collision AND
/// confusion at the call site.
/// </para>
/// <para>
/// <strong>Precedence</strong> (matches BCL <c>[AllowAnonymous]</c> over
/// <c>[Authorize]</c>): a method-level <see cref="D2AllowAnonymousAttribute"/>
/// overrides any class-level <see cref="D2RequireScopeAttribute"/> on the
/// same service. Fluent metadata
/// (<c>MethodScopeMetadata.Anonymous</c> attached via the builder extensions)
/// takes precedence over both attribute paths.
/// </para>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Class,
    AllowMultiple = false,
    Inherited = false)]
public sealed class D2AllowAnonymousAttribute : Attribute
{
}
