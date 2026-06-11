// -----------------------------------------------------------------------
// <copyright file="IGetRotationPlanHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;

using D2.Shared.Handler.Abstractions;

/// <summary>
/// Computes the read-only plan of lifecycle actions due across all key domains
/// (bootstrap / activate / rotate / generate-successor / retire).
/// </summary>
public interface IGetRotationPlanHandler : IHandler<GetRotationPlanInput, GetRotationPlanOutput>;
