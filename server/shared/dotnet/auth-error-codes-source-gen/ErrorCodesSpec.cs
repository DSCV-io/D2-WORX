// -----------------------------------------------------------------------
// <copyright file="ErrorCodesSpec.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.ErrorCodes.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of <c>contracts/auth-error-codes/auth-error-codes.spec.json</c>.
/// The <c>$schema</c> field is intentionally absent — JSON-Schema validation
/// happens at edit time in editors / IDEs; the loader just deserializes the
/// data fields and validates them in <see cref="ErrorCodesEmitter"/>.
/// </summary>
/// <param name="ErrorCodes">Every error-code entry declared in the spec (in spec order).</param>
internal sealed record ErrorCodesSpec(ImmutableArray<ErrorCodeEntry> ErrorCodes);
