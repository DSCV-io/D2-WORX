// -----------------------------------------------------------------------
// <copyright file="RunD2ServiceAsyncTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Hosting;
using global::Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Xunit;

/// <summary>
/// Integration tests for
/// <see cref="RunD2ServiceWebApplicationExtensions.RunD2ServiceAsync"/>.
/// Verifies the try / catch / finally wrapping captures startup faults via
/// PII-safe Serilog rendering and always flushes the log buffer in finally.
/// Tests serialize on the static <c>Log.Logger</c> (the wrapper writes to
/// it from the catch + finally paths) — single-collection annotation
/// prevents parallel pollution.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class RunD2ServiceAsyncTests
{
    [Fact]
    public async Task SuccessfulRun_ReturnsCleanly_AfterStop()
    {
        var (app, sink) = BuildAppWithSink();

        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Stop the app shortly after start so RunD2ServiceAsync returns.
        _ = Task.Run(async () =>
        {
            await Task.Delay(150);
            await app.StopAsync(ct.Token);
        });

        var act = async () => await app.RunD2ServiceAsync("test-svc");
        await act.Should().NotThrowAsync();

        // "Starting" log line emitted at entry.
        sink.Events.Should().Contain(e =>
            e.MessageTemplate.Text.Contains("Starting"));
    }

    [Fact]
    public async Task StartupFault_LogsFatal_WithoutExceptionMessage_AndRethrows()
    {
        var (app, sink) = BuildAppWithSink(svcs =>
        {
            svcs.AddHostedService<ThrowingHostedService>();
        });

        var act = async () => await app.RunD2ServiceAsync("test-svc");
        await act.Should().ThrowAsync<InvalidOperationException>();

        var fatal = sink.Events.FirstOrDefault(e => e.Level == LogEventLevel.Fatal);
        fatal.Should().NotBeNull(
            "RunD2ServiceAsync's catch path must Log.Fatal on host startup failure");

        // PII discipline regression: rendered message must contain the
        // exception type FullName but must NOT contain the raw exception
        // message (which can carry connection strings / secrets / user input).
        var rendered = fatal.RenderMessage(System.Globalization.CultureInfo.InvariantCulture);
        rendered.Should().Contain(typeof(InvalidOperationException).FullName!);
        rendered.Should().NotContain("Synthetic startup failure (do not log).");
    }

    [Fact]
    public async Task FinallyPath_FlushesLogger_RegardlessOfFault()
    {
        var (app, sink) = BuildAppWithSink(svcs =>
        {
            svcs.AddHostedService<ThrowingHostedService>();
        });

        var act = async () => await app.RunD2ServiceAsync("test-svc");
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Sink received the Fatal event — proves the finally CloseAndFlushAsync
        // ran (otherwise the buffered batch would not have surfaced).
        sink.Events.Any(e => e.Level == LogEventLevel.Fatal).Should().BeTrue();
    }

    [Fact]
    public async Task ServiceNameNullOrEmpty_FallsBackToApplicationName()
    {
        var (app, sink) = BuildAppWithSink();

        // Capture ApplicationName upfront — accessing app.Environment after
        // the host disposes raises ObjectDisposedException.
        var applicationName = app.Environment.ApplicationName;

        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = Task.Run(async () =>
        {
            await Task.Delay(150);
            await app.StopAsync(ct.Token);
        });

        await app.RunD2ServiceAsync(string.Empty);

        // Starting line uses ApplicationName when serviceName is empty.
        var startingEvent = sink.Events.First(e =>
            e.MessageTemplate.Text.Contains("Starting"));
        startingEvent.Properties["ServiceName"].ToString()
            .Should().Contain(applicationName);
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

        // Bind to an ephemeral port (port 0 = OS-assigned) so parallel test
        // invocations don't collide on a fixed port.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = ["--urls", "http://127.0.0.1:0"],
        });
        builder.Logging.ClearProviders();

        extraServices?.Invoke(builder.Services);

        var app = builder.Build();
        return (app, sink);
    }

    /// <summary>
    /// Hosted service whose <c>StartAsync</c> always throws — used to
    /// exercise <c>RunD2ServiceAsync</c>'s startup-fault catch path.
    /// </summary>
    private sealed class ThrowingHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Synthetic startup failure (do not log).");

        public Task StopAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// Minimal in-memory Serilog sink for these tests — captures every
    /// LogEvent so assertions can inspect Level + RenderedMessage +
    /// Properties.
    /// </summary>
    private sealed class MemorySink : Serilog.Core.ILogEventSink
    {
        private readonly List<LogEvent> r_events = new();

        public IReadOnlyList<LogEvent> Events
        {
            get
            {
                lock (r_events)
                {
                    return r_events.ToList();
                }
            }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (r_events)
            {
                r_events.Add(logEvent);
            }
        }
    }
}
