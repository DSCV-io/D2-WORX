// -----------------------------------------------------------------------
// <copyright file="Measurement.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Handler;

internal sealed record Measurement(string InstrumentName, object Value);
