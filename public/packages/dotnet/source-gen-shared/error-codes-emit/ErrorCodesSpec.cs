// -----------------------------------------------------------------------
// <copyright file="ErrorCodesSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of a <c>*-error-codes.spec.json</c> catalog. The
/// <c>$schema</c> field is intentionally absent — JSON-Schema validation
/// happens at edit time in editors / IDEs; the loader just deserializes the
/// data fields and validates them in <see cref="ConstantsEmitter"/>.
/// </summary>
/// <param name="ErrorCodes">Every error-code entry declared in the spec (in spec order).</param>
internal sealed record ErrorCodesSpec(ImmutableArray<ErrorCodeEntry> ErrorCodes);
