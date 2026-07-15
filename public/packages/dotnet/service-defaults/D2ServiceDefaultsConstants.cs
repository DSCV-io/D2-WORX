// -----------------------------------------------------------------------
// <copyright file="D2ServiceDefaultsConstants.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ServiceDefaults;

/// <summary>
/// Public constants exposed by <see cref="D2.Shared.ServiceDefaults"/>.
/// Currently empty — this aggregator owns ZERO logic and reads no env vars
/// of its own; every behavior is delegated to a prior shared lib whose
/// constants live on its own <c>D2*Constants</c> class.
/// </summary>
/// <remarks>
/// Exists as an explicit placeholder so the per-lib constants convention
/// is visible from the moment the csproj exists. If a future env-var key
/// IS introduced at this aggregator layer, it lives here.
/// </remarks>
public static class D2ServiceDefaultsConstants
{
}
