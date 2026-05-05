// -----------------------------------------------------------------------
// <copyright file="Measurement.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Handler;

internal sealed record Measurement(string InstrumentName, object Value);
