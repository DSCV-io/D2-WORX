// -----------------------------------------------------------------------
// <copyright file="ScopesSpec.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Scopes.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of <c>contracts/auth-scopes/scopes.spec.json</c>. The
/// <c>$schema</c> field is intentionally absent — JSON-Schema validation
/// happens at edit time in editors / IDEs; the loader just deserializes the
/// data fields and validates them in <see cref="ScopesEmitter"/>.
/// </summary>
/// <param name="Scopes">Every scope declared in the spec (in spec order).</param>
internal sealed record ScopesSpec(ImmutableArray<ScopeEntry> Scopes);
