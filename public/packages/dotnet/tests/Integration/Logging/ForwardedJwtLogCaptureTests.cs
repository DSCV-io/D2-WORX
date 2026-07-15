// -----------------------------------------------------------------------
// <copyright file="ForwardedJwtLogCaptureTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Logging;

using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Logging.Destructuring;
using D2.Shared.Tests.Integration.Logging.Infrastructure;
using Serilog;
using Serilog.Events;
using Xunit;

/// <summary>
/// End-to-end log-capture pin for <see cref="ForwardedJwt"/>: drives a real
/// Serilog logger (with the <see cref="RedactDataDestructuringPolicy"/>
/// registered, exactly as production wires it) across a capture-then-reveal
/// cycle and asserts the raw bearer bytes appear in NO emitted event —
/// exercising BOTH the structural (<c>{@x}</c>) destructuring path AND the plain
/// (<c>{x}</c>) ToString path. Mirrors the <c>SerilogPipelineRedactionTests</c>
/// logger+sink shape.
/// </summary>
/// <remarks>
/// The token is captured into a real <see cref="MutableForwardedJwtAccessor"/>
/// (the production holder) and then revealed via
/// <see cref="ForwardedJwt.RevealForForwarding"/> — the forward leg the outbound
/// credential performs. The reveal is exercised here so the test proves that even
/// AFTER the bytes are revealed for forwarding, nothing logged them.
/// </remarks>
public sealed class ForwardedJwtLogCaptureTests
{
    // Distinctive, JWT-shaped sentinel — a substring hit anywhere in the
    // rendered output is unambiguous evidence of a leak.
    private const string _KNOWN_JWT =
        "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJMT0dfQ0FQVFVSRV9TRU5USU5FTCJ9.LeAkSeNtInEl_99";

    [Fact]
    public void ForwardedJwt_LoggedEveryWay_BytesNeverSurface_PlaceholderDoes()
    {
        var (logger, sink) = BuildLogger();

        // Capture into the real holder, then play the consumer under test: read it
        // back and reveal the bytes (as the outbound credential will).
        var holder = new MutableForwardedJwtAccessor();
        holder.Capture(_KNOWN_JWT);
        var current = holder.Current!.Value;

        // (1) Structural destructuring — RedactDataDestructuringPolicy masks the
        // type-level [RedactData] to "[REDACTED: SecretInformation]".
        logger.Information("holder-structural {@Holder}", current);

        // (2) Plain ToString path — bypasses the policy; the wrapper's own
        // ToString() must yield the placeholder.
        logger.Information("holder-plain {Holder}", current);

        // (3) String-interpolation path — also routes through ToString().
        logger.Information("holder-interp token={Token}", $"{current}");

        // The "forward" leg — reveal the bytes; even post-reveal, nothing logged.
        var revealed = current.RevealForForwarding();
        revealed.Should().Be(_KNOWN_JWT);

        var rendered = string.Join("\n", sink.Events.Select(sink.Render));

        // The raw bytes appear NOWHERE — across every event, every path.
        rendered.Should().NotContain(_KNOWN_JWT);
        rendered.Should().NotContain("LOG_CAPTURE_SENTINEL");
        rendered.Should().NotContain("LeAkSeNtInEl");

        // And the placeholder DOES appear (absence of bytes is redaction, not
        // a no-op): the structural path emits the policy placeholder, the plain
        // + interpolation paths emit the wrapper's ToString placeholder.
        rendered.Should().Contain($"[REDACTED: {D2.Shared.Utilities.Enums.RedactReason.SecretInformation}]");
        rendered.Should().Contain(ForwardedJwt.REDACTION_PLACEHOLDER);
    }

    private static (ILogger Logger, InMemorySink Sink) BuildLogger()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Destructure.With<RedactDataDestructuringPolicy>()
            .WriteTo.Sink(sink, restrictedToMinimumLevel: LogEventLevel.Verbose)
            .CreateLogger();
        return (logger, sink);
    }
}
