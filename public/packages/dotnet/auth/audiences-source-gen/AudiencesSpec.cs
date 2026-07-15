// -----------------------------------------------------------------------
// <copyright file="AudiencesSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Audiences.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of <c>contracts/auth-audiences/audiences.spec.json</c>. The
/// <c>$schema</c> field is intentionally absent — JSON-Schema validation
/// happens at edit time in editors / IDEs; the loader just deserializes the
/// data fields and validates them in <see cref="AudiencesEmitter"/>.
/// </summary>
/// <param name="Audiences">Every audience declared in the spec (in spec order).</param>
internal sealed record AudiencesSpec(ImmutableArray<AudienceEntry> Audiences);
