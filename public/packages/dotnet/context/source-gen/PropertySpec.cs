// -----------------------------------------------------------------------
// <copyright file="PropertySpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

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
/// <param name="Propagate">
/// When true, this property is included in the codegen-emitted
/// <c>PropagatedContext</c> record (the cross-hop subset that ships in the
/// <c>x-d2-context</c> AMQP / gRPC / HTTP header). Identity fields
/// (UserId / OrgId / Scopes / ActorChain) MUST NOT be propagated — they
/// rebuild from the JWT at every sync hop.
/// </param>
/// <param name="MaxLength">
/// Wire-level per-field length cap, enforced by the codegen-emitted
/// <c>PropagatedContextSerializer.TryDecode</c>. A forged
/// <c>x-d2-context</c> header with any propagatable field exceeding its
/// cap is dropped wholesale — propagation is opportunistic, never required.
/// Only meaningful when <see cref="Propagate"/> is true on a string-typed
/// field.
/// </param>
/// <param name="EntryIdMaxLength">
/// Per-entry id length cap for a propagated list-of-records field (e.g.
/// <c>CallPath</c>). Bounds a single forged entry id so it cannot bloat log
/// scope keys / audit columns even when the entry count is within
/// <see cref="MaxLength"/>. Single source of the cap: the codegen-emitted
/// <c>PropagatedContextSerializer</c> (both .NET and TypeScript) derives it
/// from this value — neither hard-codes the number. Only meaningful on a
/// propagated list-of-records field.
/// </param>
/// <param name="Redact">
/// When true, the property is PII-bearing and must be redacted from logs
/// and telemetry. The emitter places <c>[RedactData]</c> on the generated
/// interface property AND the matching property on
/// <c>MutableRequestContext</c>; the Serilog destructuring policy
/// (<c>DcsvIo.D2.Logging.Destructuring.RedactDataDestructuringPolicy</c>)
/// reflects over the concrete type at log time. The TS-side codegen
/// emits the same property name into a <c>RedactPaths</c> array; cross-
/// spec parity is enforced by the
/// <c>RedactDataVsSpecRedactConsistencyTests</c> gate.
/// </param>
internal sealed record PropertySpec(
    string Name,
    string Type,
    string? Claim,
    bool TrinaryAuth,
    string? Derived,
    string? Default,
    string? Doc,
    bool Propagate,
    int? MaxLength,
    int? EntryIdMaxLength,
    bool Redact);
