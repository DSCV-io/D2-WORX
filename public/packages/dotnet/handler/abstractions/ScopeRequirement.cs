// -----------------------------------------------------------------------
// <copyright file="ScopeRequirement.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Abstractions;

using System.Collections.Generic;

/// <summary>
/// Declares the per-handler scope requirement evaluated by
/// <c>BaseHandler.RunCorePipelineAsync</c> before <c>ExecuteAsync</c> runs.
/// Combines the scope set with an explicit <see cref="HandlerScopeMatch"/> mode
/// so the any-of vs all-of semantic is always stated at declaration time,
/// mirroring the transport-layer <c>EndpointScopeMetadata</c> /
/// <c>MethodScopeMetadata</c> shape.
/// </summary>
/// <remarks>
/// <para>
/// A <see langword="null"/> <see cref="HandlerOptions.ScopeRequirement"/> disables
/// the per-handler scope pre-check entirely — any authenticated caller (that passed
/// the transport-layer auth middleware / interceptor) may invoke the handler. This
/// is the correct choice when the handler performs its own internal authorization
/// or when scope enforcement is fully delegated to the transport layer.
/// </para>
/// <para>
/// An empty <see cref="Scopes"/> set is illegal at construction time
/// (<see cref="ArgumentException"/> thrown) — use a <see langword="null"/>
/// <see cref="HandlerOptions.ScopeRequirement"/> instead to disable the check.
/// The <c>BaseHandler</c> pipeline guard (<c>is { Scopes.Count: &gt; 0 }</c>)
/// remains as defense-in-depth, but the constructor guard surfaces the
/// misconfiguration at the earliest possible moment (DI composition /
/// option-record creation) rather than silently at call time.
/// </para>
/// </remarks>
public sealed record ScopeRequirement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeRequirement"/> record.
    /// </summary>
    /// <param name="match">
    /// Whether the caller must hold any one of the scopes
    /// (<see cref="HandlerScopeMatch.Any"/>) or every scope
    /// (<see cref="HandlerScopeMatch.All"/>).
    /// </param>
    /// <param name="scopes">
    /// The scope set the caller must satisfy. Must contain at least one entry.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="scopes"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="scopes"/> is empty. Use a <see langword="null"/>
    /// <see cref="HandlerOptions.ScopeRequirement"/> to disable the per-handler check.
    /// </exception>
    public ScopeRequirement(HandlerScopeMatch match, IReadOnlySet<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        // DcsvIo.D2.Utilities is not referenced by handler/abstractions; BCL
        // check is the correct approach here.
        if (scopes.Count == 0)
        {
            throw new ArgumentException(
                "ScopeRequirement.Scopes must contain at least one entry; "
                    + "use a null ScopeRequirement to disable the per-handler check.",
                nameof(scopes));
        }

        Match = match;
        Scopes = scopes;
    }

    /// <summary>
    /// Gets whether the caller must hold any one of the scopes
    /// (<see cref="HandlerScopeMatch.Any"/>) or every scope
    /// (<see cref="HandlerScopeMatch.All"/>).
    /// </summary>
    public HandlerScopeMatch Match { get; }

    /// <summary>
    /// Gets the scope set the caller must satisfy. Always non-empty; how this
    /// set is evaluated against the caller's granted scopes depends on
    /// <see cref="Match"/>.
    /// </summary>
    public IReadOnlySet<string> Scopes { get; }
}
