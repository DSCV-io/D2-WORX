// -----------------------------------------------------------------------
// <copyright file="RunD2ServiceAsyncE2ETests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ServiceDefaults;

using AwesomeAssertions;
using DcsvIo.D2.ServiceDefaults;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Hosting;
using global::Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

/// <summary>
/// Verifies the
/// <see cref="WebApplicationServiceDefaultsExtensions.RunD2ServiceAsync"/>
/// re-export at the aggregator namespace — pins that the AspNetCore
/// startup wrapper's "Starting" log line, PII-safe Log.Fatal on startup
/// fault, rethrow, and Log.CloseAndFlushAsync in finally all fire when
/// invoked through the composed aggregator (mirroring the contract held
/// by the underlying <c>DcsvIo.D2.AspNetCore</c> direct path).
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class RunD2ServiceAsyncE2ETests
{
    [Fact]
    public async Task RunD2ServiceAsync_StartingLine_EmittedWithServiceName()
    {
        var (app, sink) = BuildAppWithSink();
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _ = Task.Run(async () =>
        {
            await Task.Delay(150, ct.Token);
            await app.StopAsync(ct.Token);
        });

        var act = async () => await app.RunD2ServiceAsync("composed-svc");
        await act.Should().NotThrowAsync();

        sink.Events.Should().Contain(e =>
            e.MessageTemplate.Text.Contains("Starting"));
        var startingEvent = sink.Events.First(e =>
            e.MessageTemplate.Text.Contains("Starting"));
        startingEvent.Properties["ServiceName"].ToString()
            .Should().Contain("composed-svc");
    }

    [Fact]
    public async Task RunD2ServiceAsync_OnStartupException_LogFatal_NoExceptionMessage_AndRethrows()
    {
        // Negative regression for §3.1 / §3.2: the catch path Log.Fatal
        // must NOT include ex.Message (which can carry connection
        // strings, secrets, user input). Pin under the aggregator
        // re-export.
        var (app, sink) = BuildAppWithSink(svcs =>
            svcs.AddHostedService<ThrowingHostedService>());

        var act = async () => await app.RunD2ServiceAsync("composed-svc");
        await act.Should().ThrowAsync<InvalidOperationException>();

        var fatal = sink.Events.FirstOrDefault(e => e.Level == LogEventLevel.Fatal);
        fatal.Should().NotBeNull(
            "RunD2ServiceAsync's catch path must Log.Fatal on host startup failure");

        var rendered = fatal.RenderMessage(
            System.Globalization.CultureInfo.InvariantCulture);
        rendered.Should().Contain(typeof(InvalidOperationException).FullName!);
        rendered.Should().NotContain(
            "Synthetic startup failure under aggregator (do not log).");
    }

    [Fact]
    public async Task RunD2ServiceAsync_FinallyPath_FlushesLogger_RegardlessOfFault()
    {
        var (app, sink) = BuildAppWithSink(svcs =>
            svcs.AddHostedService<ThrowingHostedService>());

        var act = async () => await app.RunD2ServiceAsync("composed-svc");
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Fatal event reached the sink → finally CloseAndFlushAsync ran
        // (otherwise the buffered batch would not have surfaced).
        sink.Events.Any(e => e.Level == LogEventLevel.Fatal).Should().BeTrue();
    }

    [Fact]
    public async Task RunD2ServiceAsync_NullApp_ThrowsArgumentNull()
    {
        WebApplication app = null!;

        var act = async () => await app.RunD2ServiceAsync("composed-svc");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static (WebApplication App, MemorySink Sink) BuildAppWithSink(
        Action<IServiceCollection>? extraServices = null)
    {
        var sink = new MemorySink();
        var localLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink, restrictedToMinimumLevel: LogEventLevel.Verbose)
            .CreateLogger();
        Log.Logger = localLogger;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // Ephemeral Kestrel port so parallel test invocations
            // don't collide on a fixed port.
            Args = ["--urls", "http://127.0.0.1:0"],
        });
        builder.Logging.ClearProviders();

        extraServices?.Invoke(builder.Services);

        var app = builder.Build();
        return (app, sink);
    }

    /// <summary>
    /// Hosted service whose <c>StartAsync</c> always throws — used to
    /// exercise <c>RunD2ServiceAsync</c>'s startup-fault catch path
    /// through the aggregator re-export.
    /// </summary>
    private sealed class ThrowingHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "Synthetic startup failure under aggregator (do not log).");

        public Task StopAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// Minimal in-memory Serilog sink — captures every LogEvent so
    /// assertions can inspect Level + RenderedMessage + Properties.
    /// </summary>
    private sealed class MemorySink : ILogEventSink
    {
        private readonly List<LogEvent> r_events = new();

        public IReadOnlyList<LogEvent> Events
        {
            get
            {
                lock (r_events)
                    return r_events.ToList();
            }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (r_events)
                r_events.Add(logEvent);
        }
    }
}
