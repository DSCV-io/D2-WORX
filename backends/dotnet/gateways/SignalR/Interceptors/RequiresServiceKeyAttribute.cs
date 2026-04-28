// -----------------------------------------------------------------------
// <copyright file="RequiresServiceKeyAttribute.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.SignalR.Interceptors;

/// <summary>
/// Marks a gRPC method as requiring service key validation.
/// The <see cref="ServiceKeyInterceptor"/> reads this from endpoint metadata.
/// Methods without this attribute pass through without API key checks.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresServiceKeyAttribute : Attribute;
