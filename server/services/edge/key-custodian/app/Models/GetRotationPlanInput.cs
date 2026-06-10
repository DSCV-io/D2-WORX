// -----------------------------------------------------------------------
// <copyright file="GetRotationPlanInput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Models;

/// <summary>
/// Input to <c>GetRotationPlan</c>. The plan is computed across all domains from
/// the store + the policy provider, so the query takes no parameters.
/// </summary>
public sealed record GetRotationPlanInput;
