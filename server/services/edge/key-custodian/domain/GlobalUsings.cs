// -----------------------------------------------------------------------
// <copyright file="GlobalUsings.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// IClock is reached via a per-file alias to avoid the NodaTime.IClock vs D2.Shared.Time.IClock
// ambiguity a global using would create.
global using D2.Edge.KeyCustodian.Domain.Entities;
global using D2.Edge.KeyCustodian.Domain.Enums;
global using D2.Edge.KeyCustodian.Domain.Errors;
global using D2.Edge.KeyCustodian.Domain.ValueObjects;
global using NodaTime;
