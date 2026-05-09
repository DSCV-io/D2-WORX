// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Encryption;

using AwesomeAssertions;
using D2.Shared.Encryption;
using Xunit;

/// <summary>
/// Pin the canonical domain string values. These constants are referenced
/// across services + ops tooling — changing them silently would route messages
/// to the wrong keyring.
/// </summary>
public sealed class EncryptionDomainsTests
{
    [Fact]
    public void Audit_HasExpectedValue()
        => EncryptionDomains.Audit.Should().Be("audit");

    [Fact]
    public void Notifications_HasExpectedValue()
        => EncryptionDomains.Notifications.Should().Be("notifications");

    [Fact]
    public void Courier_HasExpectedValue()
        => EncryptionDomains.Courier.Should().Be("courier");
}
