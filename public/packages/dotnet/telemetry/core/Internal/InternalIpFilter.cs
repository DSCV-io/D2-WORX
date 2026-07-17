// -----------------------------------------------------------------------
// <copyright file="InternalIpFilter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Internal;

using System.Net;

/// <summary>
/// Internal helper that decides whether a connection-remote
/// <see cref="IPAddress"/> is "internal" — loopback or RFC 1918 private
/// address — for the
/// <see cref="WebApplicationTelemetryExtensions.MapD2PrometheusEndpoint"/>
/// IP allow-list filter. Hosts behind reverse proxies are expected to
/// surface the proxy's own IP at the connection layer; the proxy IS the
/// loopback / RFC 1918 source, the original client IP is not (and is not
/// a metrics consumer anyway).
/// </summary>
/// <remarks>
/// Hard-coded byte-prefix checks (no <c>IPNetwork</c> dep) per v1
/// baseline. Covers Docker bridge, Kubernetes pod networks, and VPS
/// private VLANs.
/// </remarks>
internal static class InternalIpFilter
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="remoteIp"/> is loopback
    /// (IPv4 <c>127.0.0.0/8</c> or IPv6 <c>::1</c>) or an RFC 1918 IPv4
    /// private range (<c>10.0.0.0/8</c>, <c>172.16.0.0/12</c>,
    /// <c>192.168.0.0/16</c>). Returns <c>false</c> for null inputs and
    /// for any IPv6 address that isn't loopback.
    /// </summary>
    /// <param name="remoteIp">The remote IP address to test.</param>
    /// <returns>
    /// <c>true</c> when the IP is allowed to scrape the Prometheus
    /// endpoint; <c>false</c> otherwise.
    /// </returns>
    internal static bool IsAllowed(IPAddress? remoteIp)
    {
        if (remoteIp is null)
            return false;

        if (IPAddress.IsLoopback(remoteIp))
            return true;

        var bytes = remoteIp.GetAddressBytes();
        if (bytes.Length != 4)
            return false;

        // 10.0.0.0/8
        if (bytes[0] == 10)
            return true;

        // 172.16.0.0/12 — covers 172.16.x.x through 172.31.x.x
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
            return true;

        return false;
    }
}
