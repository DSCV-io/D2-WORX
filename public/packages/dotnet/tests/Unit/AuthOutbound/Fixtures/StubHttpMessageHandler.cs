// -----------------------------------------------------------------------
// <copyright file="StubHttpMessageHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AuthOutbound.Fixtures;

/// <summary>
/// Test-only <see cref="HttpMessageHandler"/> that delegates every outbound
/// request to a caller-supplied async handler. Lets HTTP-client tests assert
/// on requests received and synthesize arbitrary responses without standing
/// up a real server.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
        r_handler;

    /// <summary>Initializes the handler with a per-request callback.</summary>
    /// <param name="handler">Async callback invoked for each outbound request.</param>
    public StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        r_handler = handler;
    }

    /// <summary>Gets the total number of requests received by this handler.</summary>
    public int RequestCount { get; private set; }

    /// <summary>Gets the most recent request observed (null if none yet).</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        RequestCount++;
        LastRequest = request;
        return await r_handler(request, ct);
    }
}
