// -----------------------------------------------------------------------
// <copyright file="PropertySpec.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Context.SourceGen;

/// <summary>
/// One property declaration within a <see cref="Section"/>. Renders as an
/// interface property + a corresponding mutable field on the concrete class
/// (and an envelope-record field, if non-derived).
/// </summary>
/// <param name="Name">PascalCase property name (e.g. <c>"OrgId"</c>).</param>
/// <param name="Type">
/// Type string from the closed vocabulary (<c>"string?"</c>, <c>"Guid?"</c>,
/// <c>"IReadOnlyList&lt;ActorEntry&gt;"</c>, etc.). The emitter validates
/// against an allow-list and emits <c>D2CTX002</c> on unknown types.
/// </param>
/// <param name="Claim">
/// JWT claim name this property maps to (used by FromClaims / FromJwtPayload
/// factories). Null for properties not sourced from JWT (e.g. transport-level
/// fields on IRequestContext, or derived properties).
/// </param>
/// <param name="TrinaryAuth">
/// True for <c>bool?</c> properties whose null-vs-false distinction is
/// meaningful (pre-auth vs confirmed-not). Defaults the property to null
/// instead of falling back to default(bool?). No structural difference at
/// codegen time — kept as documentation + future-proofing for emit-time
/// behavior toggles.
/// </param>
/// <param name="Derived">
/// Marks this property as computed from other properties at read-time. The
/// named rule is implemented by the generator's <c>MutableEmitter</c>.
/// Currently recognized: <c>"actorChain"</c> (walks the actor chain to
/// compute impersonation flavor / impersonator org / etc.).
/// </param>
/// <param name="Default">
/// Default-value expression (raw C# literal). Examples: <c>"[]"</c> for
/// collection-expression empty defaults, <c>"null"</c> for explicit null.
/// </param>
/// <param name="Doc">
/// XML doc <c>&lt;summary&gt;</c> text rendered on the generated property.
/// </param>
internal sealed record PropertySpec(
    string Name,
    string Type,
    string? Claim,
    bool TrinaryAuth,
    string? Derived,
    string? Default,
    string? Doc);
