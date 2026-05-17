// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Encryption;

using System.Linq;
using AwesomeAssertions;
using D2.Shared.Encryption;
using Xunit;

/// <summary>
/// Pin the canonical encryption-domain wire values. These constants are
/// referenced across services + ops tooling — changing them silently would
/// route messages to the wrong keyring. The catalog is spec-driven and
/// codegen-emitted; these tests assert the emitted constants haven't drifted
/// from the values the consumer base depends on.
/// </summary>
public sealed class EncryptionDomainsTests
{
    [Fact]
    public void Audit_HasExpectedValue()
        => EncryptionDomains.AUDIT.Should().Be("audit");

    [Fact]
    public void Notifications_HasExpectedValue()
        => EncryptionDomains.NOTIFICATIONS.Should().Be("notifications");

    [Fact]
    public void Courier_HasExpectedValue()
        => EncryptionDomains.COURIER.Should().Be("courier");

    [Fact]
    public void Plaintext_HasExpectedValue()
        => EncryptionDomains.PLAINTEXT.Should().Be("plaintext");

    [Fact]
    public void AllDomains_EnumeratesEveryEntryInSpecOrder()
        => EncryptionDomains.AllDomains.Should()
            .Equal("audit", "notifications", "courier", "plaintext");

    [Fact]
    public void AllDomains_ContainsNoDuplicates()
        => EncryptionDomains.AllDomains
            .Distinct()
            .Should()
            .HaveCount(EncryptionDomains.AllDomains.Count);
}
