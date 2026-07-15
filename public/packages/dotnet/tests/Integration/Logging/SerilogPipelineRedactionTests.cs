// -----------------------------------------------------------------------
// <copyright file="SerilogPipelineRedactionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Logging;

using AwesomeAssertions;
using DcsvIo.D2.Logging.Destructuring;
using DcsvIo.D2.Tests.Integration.Logging.Infrastructure;
using Serilog;
using Serilog.Events;
using Xunit;
using static DcsvIo.D2.Tests.Integration.Logging.Infrastructure.IntegrationRedactionFixtures;

/// <summary>
/// End-to-end Serilog-pipeline redaction coverage. Logs go through a real
/// Serilog logger configured with the
/// <see cref="RedactDataDestructuringPolicy"/> + the in-memory sink, then
/// assertions run against the captured event AND the rendered JSON output.
/// Catches wiring regressions (e.g. <c>Destructure.With&lt;...&gt;</c>
/// accidentally dropped) that pure unit tests on the policy itself would
/// miss.
/// </summary>
public sealed class SerilogPipelineRedactionTests
{
    [Fact]
    public void TypeLevelRedaction_OutputContainsPlaceholder()
    {
        var (logger, sink) = BuildLogger();
        var pii = new TypeLevelPii("alice@example.com", "555-0100");

        logger.Information("captured {@User}", pii);

        var rendered = Render(sink);
        rendered.Should().Contain("[REDACTED: PersonalInformation]");
        rendered.Should().NotContain("alice@example.com");
        rendered.Should().NotContain("555-0100");
    }

    [Fact]
    public void TypeLevelRedaction_CustomReason_OutputContainsCustomReasonString()
    {
        var (logger, sink) = BuildLogger();
        var pii = new TypeLevelCustomReason("ssh-rsa AAAAB...");

        logger.Information("captured {@Secret}", pii);

        var rendered = Render(sink);
        rendered.Should().Contain("[REDACTED: MyVeryCustomReason]");
        rendered.Should().NotContain("ssh-rsa");
    }

    [Fact]
    public void PropertyLevelRedaction_OutputMasksPropertyButPreservesNonRedacted()
    {
        var (logger, sink) = BuildLogger();
        var record = new PropertyLevelMixed
        {
            PublicName = "alice",
            SecretEmail = "alice@example.com",
        };

        logger.Information("captured {@Record}", record);

        var rendered = Render(sink);
        rendered.Should().Contain("[REDACTED: PersonalInformation]");
        rendered.Should().Contain("alice");
        rendered.Should().NotContain("alice@example.com");
    }

    [Fact]
    public void CollectionOfRedactedTypes_EachElementMasked()
    {
        var (logger, sink) = BuildLogger();
        var batch = new OuterWithListOfPii
        {
            Description = "batch",
            Items =
            [
                new TypeLevelPii("a@a.co", "111"),
                new TypeLevelPii("b@b.co", "222"),
            ],
        };

        logger.Information("captured {@Batch}", batch);

        var rendered = Render(sink);
        rendered.Should().Contain("batch");
        rendered.Should().NotContain("a@a.co");
        rendered.Should().NotContain("b@b.co");
        const string placeholder = "[REDACTED: PersonalInformation]";
        var occurrences = 0;
        var idx = 0;
        while ((idx = rendered.IndexOf(placeholder, idx, StringComparison.Ordinal)) >= 0)
        {
            occurrences++;
            idx += placeholder.Length;
        }

        occurrences.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void CollectionOfNestedRecords_PropertyLevelRedactedBytes_EachElementMasked_KidIntact()
    {
        // The exact emitted GetKeyringOutput/KeyringEntry shape: a collection of nested
        // records, each carrying a property-level [RedactData(SecretInformation)] byte[]
        // KeyBytes plus a plain Kid. Every element's KeyBytes must mask; every Kid stays.
        var (logger, sink) = BuildLogger();
        var output = new OuterWithKeyringEntriesFixture
        {
            ActiveKid = "kid-alpha",
            Entries =
            [
                new KeyringEntryFixture("kid-alpha", [1, 2, 3, 4]),
                new KeyringEntryFixture("kid-beta", [5, 6, 7, 8]),
            ],
        };

        logger.Information("captured {@Keyring}", output);

        var rendered = Render(sink);
        rendered.Should().Contain("kid-alpha");
        rendered.Should().Contain("kid-beta");

        const string placeholder = "[REDACTED: SecretInformation]";
        var occurrences = 0;
        var idx = 0;
        while ((idx = rendered.IndexOf(placeholder, idx, StringComparison.Ordinal)) >= 0)
        {
            occurrences++;
            idx += placeholder.Length;
        }

        occurrences.Should().BeGreaterThanOrEqualTo(
            2, "each nested KeyringEntry element's KeyBytes must be masked independently");
    }

    [Fact]
    public void NoRedaction_OutputContainsRawValues()
    {
        var (logger, sink) = BuildLogger();
        var passthrough = new PassthroughRecord("something", 42);

        logger.Information("captured {@Record}", passthrough);

        var rendered = Render(sink);
        rendered.Should().Contain("something");
        rendered.Should().Contain("42");
        rendered.Should().NotContain("REDACTED");
    }

    [Fact]
    public void StringifyCaptureMode_BypassesDestructuringAndLeaksValue()
    {
        // Documented carve-out: {prop} (no @) calls .ToString() on the value
        // and bypasses the destructuring policy entirely. This test pins the
        // README's "Destructuring discipline" warning — capture-mode matters.
        var (logger, sink) = BuildLogger();
        var pii = new TypeLevelPii("leaked@example.com", "555-0100");

        // Without @ — bypasses the policy. The .ToString() of a record
        // includes the property values; we assert the leak to make the
        // carve-out testable.
        logger.Information("captured {Record}", pii);

        var rendered = Render(sink);
        rendered.Should().Contain("leaked@example.com");
        rendered.Should().NotContain("REDACTED");
    }

    [Fact]
    public void DestructureCaptureMode_AppliesRedaction()
    {
        // Counterpart to the bypass test — the same payload with @-capture
        // is masked. Together they pin the discipline contract.
        var (logger, sink) = BuildLogger();
        var pii = new TypeLevelPii("leaked@example.com", "555-0100");

        logger.Information("captured {@Record}", pii);

        var rendered = Render(sink);
        rendered.Should().NotContain("leaked@example.com");
        rendered.Should().Contain("[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void EachRedactReason_RendersItsEnumNameInPlaceholder()
    {
        var (logger, sink) = BuildLogger();

        logger.Information("u {@U}", new UnspecifiedFixture("v"));
        logger.Information("p {@P}", new PersonalInformationFixture("v"));
        logger.Information("f {@F}", new FinancialInformationFixture("v"));
        logger.Information("s {@S}", new SecretInformationFixture("v"));
        logger.Information("vc {@V}", new VerboseContentFixture("v"));
        logger.Information("o {@O}", new OtherFixture("v"));

        var rendered = string.Join("\n", sink.Events.Select(sink.Render));
        rendered.Should().Contain("[REDACTED: Unspecified]");
        rendered.Should().Contain("[REDACTED: PersonalInformation]");
        rendered.Should().Contain("[REDACTED: FinancialInformation]");
        rendered.Should().Contain("[REDACTED: SecretInformation]");
        rendered.Should().Contain("[REDACTED: VerboseContent]");
        rendered.Should().Contain("[REDACTED: Other]");
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

    private static string Render(InMemorySink sink)
        => string.Join("\n", sink.Events.Select(sink.Render));
}
