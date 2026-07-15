// -----------------------------------------------------------------------
// <copyright file="FixturePayloadDomains.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

using D2.Edge.KeyCustodian.Domain.ValueObjects;

/// <summary>
/// Test-only symmetric AES-payload fixture domains. After the sealed-domain catalog removal
/// (audit / notifications / courier flipped to sealed and left the KC symmetric payload
/// catalog) NO production AES-payload domain remains, so a test that must exercise the
/// preserved domain-generic symmetric machinery (getKeyring op + authority + validator +
/// consumer runtime) registers one of these fixture values through the
/// <see cref="KeyDomain.RegisterFixturePayloadDomainForTesting"/> seam.
/// </summary>
/// <remarks>
/// Every value carries a §7.23 fixture marker in the value itself
/// (<c>payload-fixture-*</c>, valid <c>[a-z0-9-]</c> grammar). Registration is SCOPED —
/// dispose the returned handle (or the <see cref="Scope"/>) to unregister, so the static
/// registry stays hermetic across tests. A test that also asserts a real sealed value's
/// rejection MUST be collection-isolated from any scope that re-admits that value.
/// </remarks>
internal static class FixturePayloadDomains
{
    /// <summary>A fixture symmetric AES-payload domain.</summary>
    public const string PAYLOAD_A = "payload-fixture-a";

    /// <summary>A second fixture symmetric AES-payload domain (distinct consumer).</summary>
    public const string PAYLOAD_B = "payload-fixture-b";

    /// <summary>
    /// Registers <paramref name="values"/> as symmetric AES-payload domains for the lifetime
    /// of the returned handle. Defaults to <see cref="PAYLOAD_A"/> when none are supplied.
    /// </summary>
    /// <param name="values">The fixture domain values to register (default: <see cref="PAYLOAD_A"/>).</param>
    /// <returns>A handle whose disposal unregisters every value (idempotent).</returns>
    public static Scope Register(params string[] values)
        => new(values.Length == 0 ? [PAYLOAD_A] : values);

    /// <summary>A scoped registration of one or more fixture payload domains.</summary>
    internal sealed class Scope : IDisposable
    {
        private readonly List<IDisposable> r_registrations;

        /// <summary>Registers each value through the KeyDomain fixture seam.</summary>
        /// <param name="values">The fixture domain values to register.</param>
        public Scope(IReadOnlyList<string> values)
        {
            r_registrations = new List<IDisposable>(values.Count);

            foreach (var value in values)
                r_registrations.Add(KeyDomain.RegisterFixturePayloadDomainForTesting(value));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var registration in r_registrations)
                registration.Dispose();

            r_registrations.Clear();
        }
    }
}
