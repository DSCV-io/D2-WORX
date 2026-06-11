// -----------------------------------------------------------------------
// <copyright file="GlobalUsings.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// Tier-2 per-layer global usings (ADR-0020 / rules.md §5.26). The domain's
// NodaTime time-type surface (every entity carries timestamps) and the service's
// own domain namespaces so entities, value objects, enums, and the generated
// error-code failures resolve without per-file usings. IClock is reached via a
// per-file alias (using IClock = D2.Shared.Time.IClock) to avoid the ambiguity
// between NodaTime.IClock and D2.Shared.Time.IClock that a global D2.Shared.Time
// using would create.
global using D2.Edge.KeyCustodian.Domain.Entities;
global using D2.Edge.KeyCustodian.Domain.Enums;
global using D2.Edge.KeyCustodian.Domain.Errors;
global using D2.Edge.KeyCustodian.Domain.ValueObjects;
global using NodaTime;
