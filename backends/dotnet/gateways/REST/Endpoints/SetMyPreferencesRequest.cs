// -----------------------------------------------------------------------
// <copyright file="SetMyPreferencesRequest.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.REST.Endpoints;

/// <summary>
/// Request body for <c>PUT /api/v1/notification-preferences</c>. Both fields
/// are optional — only provided values are written; omitted fields are left
/// unchanged on the saved preference row.
/// </summary>
/// <param name="EmailEnabled">When set, updates the email-channel toggle.</param>
/// <param name="SmsEnabled">When set, updates the SMS-channel toggle.</param>
public sealed record SetMyPreferencesRequest(bool? EmailEnabled, bool? SmsEnabled);
