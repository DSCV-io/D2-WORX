// -----------------------------------------------------------------------
// <copyright file="D2RequireScopeAttribute.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Endpoints;

using System.Collections.Generic;

/// <summary>
/// Declares that a gRPC method (or every method on a service class) requires
/// the caller's <c>IRequestContext.Scopes</c> set to overlap with at least one
/// of the listed scopes. Read by the auth interceptor via the matched
/// endpoint's metadata collection (ASP.NET routing auto-pulls method-level
/// + class-level <see cref="Attribute"/> declarations onto endpoint metadata
/// during <c>MapGrpcService&lt;T&gt;()</c>).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Precedence</strong> (matches BCL <c>[Authorize]</c> /
/// <c>[AllowAnonymous]</c>): a method-level attribute overrides any
/// class-level attribute on the same service. A method-level
/// <see cref="D2HarmlessEndpointAttribute"/> overrides any class-level
/// <see cref="D2RequireScopeAttribute"/>. Fluent metadata
/// (<c>MethodScopeMetadata</c> attached via the builder extensions) takes
/// precedence over both.
/// </para>
/// <para>
/// Defense-in-depth at the transport boundary —
/// <c>BaseHandler.RequiredScopes</c> still re-checks per-handler.
/// </para>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Class,
    AllowMultiple = false,
    Inherited = false)]
public sealed class D2RequireScopeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="D2RequireScopeAttribute"/>
    /// class with the given scope set.
    /// </summary>
    /// <param name="scope">The first required scope (at-least-one).</param>
    /// <param name="additionalScopes">Additional scopes (at-least-one).</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="additionalScopes"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="scope"/> is empty / whitespace, or any entry
    /// in <paramref name="additionalScopes"/> is empty / whitespace.
    /// </exception>
    public D2RequireScopeAttribute(string scope, params string[] additionalScopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(additionalScopes);

        for (var i = 0; i < additionalScopes.Length; i++)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                additionalScopes[i],
                $"{nameof(additionalScopes)}[{i}]");
        }

        var all = new string[additionalScopes.Length + 1];
        all[0] = scope;
        additionalScopes.CopyTo(all, 1);
        Scopes = all;
    }

    /// <summary>
    /// Gets the declared scope set (any-of). Order preserved from the
    /// constructor; deduping is performed when projected into
    /// <see cref="MethodScopeMetadata.ForScopes(IEnumerable{string})"/>.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; }
}
