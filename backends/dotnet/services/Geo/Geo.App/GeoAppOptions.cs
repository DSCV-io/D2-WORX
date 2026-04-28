// -----------------------------------------------------------------------
// <copyright file="GeoAppOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App;

/// <summary>
/// Configuration options for the Geo Application layer.
/// </summary>
public class GeoAppOptions
{
    /// <summary>
    /// Gets or sets the duration after which Location data expires in cache.
    /// </summary>
    public TimeSpan LocationExpirationDuration { get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// Gets or sets the duration after which WhoIs data expires in cache.
    /// </summary>
    public TimeSpan WhoIsExpirationDuration { get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// Gets or sets the duration after which Contact data expires in cache.
    /// </summary>
    public TimeSpan ContactExpirationDuration { get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// Gets or sets the API key → allowed context keys mapping for contact operations.
    /// Keys are API key strings, values are lists of context keys the caller may access.
    /// </summary>
    /// <example>
    /// { "auth-service-key-abc": ["org_contact", "user"], "billing-service-key-xyz": ["billing_contact"] }.
    /// </example>
    public Dictionary<string, List<string>> ApiKeyMappings { get; set; } = [];

    /// <summary>
    /// Gets or sets the distributed lock TTL in seconds for scheduled job execution.
    /// </summary>
    /// <remarks>
    /// Default is 300 seconds (5 minutes).
    /// </remarks>
    public int JobLockTtlSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the WhoIs record retention period in days.
    /// Records older than this are eligible for purge.
    /// </summary>
    /// <remarks>
    /// Default is 180 days (approximately 6 months).
    /// WhoIs records are content-addressable with Year/Month fields.
    /// Staleness is determined by comparing Year/Month against the cutoff date.
    /// </remarks>
    public int WhoIsRetentionDays { get; set; } = 180;
}
