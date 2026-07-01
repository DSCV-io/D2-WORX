// -----------------------------------------------------------------------
// <copyright file="TypeSpecDtoValidationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecDto;

using AwesomeAssertions;
using D2.Shared.Logging.Destructuring;
using Serilog;
using Serilog.Events;
using Xunit;

using GenSignFixtureInput = D2.Edge.Tests.TypeSpecDto.Generated.SignFixtureInput;

/// <summary>
/// Validates that the TypeSpec-emitted <c>sign</c> fixture DTO wires
/// <c>[property: RedactData]</c> correctly so the real
/// <see cref="RedactDataDestructuringPolicy"/> masks the <c>Payload</c>
/// property at log time.
///
/// Generated fixtures live in Unit/KeyCustodian/TypeSpecDto/Generated/*.g.cs,
/// in namespace D2.Edge.Tests.TypeSpecDto.Generated.
/// GetJwks DTO structural-equivalence tests live in
/// Unit/KeyCustodian/Clients/GetJwksTransportDtoTests.cs (those types now
/// reside in D2.Edge.KeyCustodian.Clients, not in this fixture namespace).
/// </summary>
public sealed class TypeSpecDtoValidationTests
{
    // -------------------------------------------------------------------------
    // Redaction through the REAL policy
    // -------------------------------------------------------------------------

    [Fact]
    public void GeneratedSignFixtureInput_PayloadProperty_IsRedactedByRealPolicy()
    {
        // Build a local Serilog logger with the real RedactDataDestructuringPolicy.
        // IVT is granted in D2.Shared.Logging.csproj so this assembly can
        // instantiate the internal policy directly (mirrors D2.Shared.Tests pattern).
        var sink = new TypeSpecDtoInMemorySink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Destructure.With<RedactDataDestructuringPolicy>()
            .WriteTo.Sink(sink, restrictedToMinimumLevel: LogEventLevel.Verbose)
            .CreateLogger();

        // Instantiate the generated record with a known secret payload.
        // The [property: RedactData] attribute on Payload must be seen by the
        // policy when it reflects over PUBLIC INSTANCE PROPERTIES (not ctor params).
        var input = new GenSignFixtureInput("test-kid-001", System.Text.Encoding.UTF8.GetBytes("SECRET_PAYLOAD"));

        logger.Information("sign input: {@Input}", input);

        var rendered = sink.RenderAll();

        // The Payload property must be redacted.
        rendered.Should().Contain("[REDACTED: SecretInformation]");

        // The raw secret must NOT appear.
        rendered.Should().NotContain("SECRET_PAYLOAD");
    }

    [Fact]
    public void GeneratedSignFixtureInput_NonRedactedField_IsNotMasked()
    {
        // Non-vacuous control: the kid field (not @d2Redact) must NOT be masked.
        var sink = new TypeSpecDtoInMemorySink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Destructure.With<RedactDataDestructuringPolicy>()
            .WriteTo.Sink(sink, restrictedToMinimumLevel: LogEventLevel.Verbose)
            .CreateLogger();

        const string knownKid = "test-kid-visibility-check";
        var input = new GenSignFixtureInput(knownKid, [0x01, 0x02]);

        logger.Information("sign input: {@Input}", input);

        var rendered = sink.RenderAll();

        // The non-redacted kid must flow through as-is.
        rendered.Should().Contain(knownKid);

        // The policy must be selective — at least one field unmasked.
        rendered.Should().Contain("[REDACTED: SecretInformation]");
    }
}
