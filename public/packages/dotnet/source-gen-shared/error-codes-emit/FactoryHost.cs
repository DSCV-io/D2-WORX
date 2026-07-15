// -----------------------------------------------------------------------
// <copyright file="FactoryHost.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

/// <summary>
/// Selects how a catalog's failure factories are emitted — orthogonal to the
/// per-entry <c>factoryShape</c> (which selects the call SIGNATURE).
/// </summary>
internal enum FactoryHost
{
    /// <summary>
    /// The generic cross-cutting catalog. The factories ARE the base — they
    /// <c>construct</c> a <c>D2Result</c> / <c>D2Result&lt;TData&gt;</c> directly
    /// and are emitted as members ONTO the <c>D2Result</c> /
    /// <c>D2Result&lt;TData&gt;</c> partial classes (plus the per-code boolean
    /// discriminators). No <c>&lt;Domain&gt;Failures</c> class is emitted — the
    /// host type IS <c>D2Result</c>.
    /// </summary>
    Base,

    /// <summary>
    /// A per-domain catalog (e.g. auth). The factories <c>delegate</c> to the
    /// <c>httpStatus</c>-selected base factory, stamping the domain code +
    /// <c>userMessageKey</c>. Emits BOTH a non-generic
    /// <c>&lt;Domain&gt;Failures</c> class (→ <c>D2Result</c>) AND a generic
    /// <c>&lt;Domain&gt;Failures&lt;T&gt;</c> class (→ <c>D2Result&lt;T&gt;</c>),
    /// both carrying identical method names.
    /// </summary>
    Domain,
}
