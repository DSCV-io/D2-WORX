// -----------------------------------------------------------------------
// <copyright file="IGetRotationPlanHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;

/// <summary>
/// Computes the read-only plan of lifecycle actions due across all key domains
/// (bootstrap / activate / rotate / generate-successor / retire).
/// </summary>
public interface IGetRotationPlanHandler : IHandler<GetRotationPlanInput, GetRotationPlanOutput>;
