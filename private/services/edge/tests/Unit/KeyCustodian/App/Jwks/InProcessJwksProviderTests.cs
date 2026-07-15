// -----------------------------------------------------------------------
// <copyright file="InProcessJwksProviderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App.Jwks;

using DcsvIo.D2.Auth.Abstractions.Jwks;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Jwks;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

/// <summary>
/// Unit tests for the issuer-host in-process <see cref="IJwksProvider"/>:
/// seeded Active jwks-signing keys, empty store fail-secure, refresh reload,
/// DB outage, corrupt SPKI, concurrency, and cancel paths.
/// </summary>
/// <remarks>
/// <para>Adversarial matrix (Surface × Category × Test) — AUTH residual JWKS:</para>
/// <list type="table">
/// <listheader><term>Surface</term><term>Category</term><description>Test</description></listheader>
/// <item><term>GetKeysAsync</term><term>happy / seeded Active</term><description>GetKeysAsync_SeededActiveSigningKey_ReturnsKidIndexedSnapshot</description></item>
/// <item><term>GetKeysAsync</term><term>Active + Retiring</term><description>GetKeysAsync_ActiveAndRetiring_IncludesBothPreferActiveOrder</description></item>
/// <item><term>GetKeysAsync</term><term>empty store → SU</term><description>GetKeysAsync_EmptyStore_ReturnsServiceUnavailable</description></item>
/// <item><term>GetKeysAsync</term><term>wrong domain filter</term><description>GetKeysAsync_NonSigningDomainKeys_Ignored_ReturnsUnavailable</description></item>
/// <item><term>GetKeysAsync</term><term>DB throw → SU</term><description>GetKeysAsync_DbThrows_ReturnsServiceUnavailable</description></item>
/// <item><term>GetKeysAsync</term><term>corrupt SPKI only → SU</term><description>GetKeysAsync_CorruptSpkiOnly_ReturnsServiceUnavailable</description></item>
/// <item><term>GetKeysAsync</term><term>corrupt + good SPKI</term><description>GetKeysAsync_CorruptSpkiSkipped_KeepsValidKey</description></item>
/// <item><term>GetKeysAsync</term><term>concurrent singleflight</term><description>GetKeysAsync_ConcurrentCalls_ShareSnapshot</description></item>
/// <item><term>GetKeysAsync</term><term>canceled CT</term><description>GetKeysAsync_CanceledToken_ThrowsOperationCanceled</description></item>
/// <item><term>RefreshAsync</term><term>reload after seed</term><description>RefreshAsync_AfterNewKeySeeded_UpdatesSnapshot</description></item>
/// <item><term>RefreshAsync</term><term>cooldown no-reload</term><description>RefreshAsync_WithinCooldown_DoesNotReload</description></item>
/// <item><term>ctor</term><term>null deps</term><description>Constructor_Null*_Throws</description></item>
/// <item><term>AddD2InProcessJwksProvider</term><term>DI resolve / null</term><description>AddD2InProcessJwksProviderTests</description></item>
/// <item><term>InProcessJwksLog</term><term>no Exception param</term><description>InProcessJwksLogTests</description></item>
/// <item><term>Oidc factory trust</term><term>accept/reject/dispose</term><description>OidcDiscoveryHttpMessageHandlerFactoryTests</description></item>
/// <item><term>Host DI Edge</term><term>InProcess type + TrustedRoot</term><description>AddD2EdgeHostDiIsolationTests</description></item>
/// <item><term>Host DI Audit</term><term>HttpJwks + TrustedRoot</term><description>AddD2AuditHostDiIsolationTests</description></item>
/// </list>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class InProcessJwksProviderTests
{
    private const string _ISSUER = "https://d2-edge:8443";

    [Fact]
    public async Task GetKeysAsync_SeededActiveSigningKey_ReturnsKidIndexedSnapshot()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var options = BuildOptions();
        var created = KcAppTestKit.SR_BaseInstant;
        var activeKid = await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created + Duration.FromHours(2));

        var provider = MakeProvider(db, options);

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeTrue();
        result.Data!.Keys.Should().ContainKey(activeKid);
        result.Data.SourceUri.Should()
            .Be(new Uri("https://d2-edge:8443/.well-known/jwks.json"));
    }

    [Fact]
    public async Task GetKeysAsync_ActiveAndRetiring_IncludesBothPreferActiveOrder()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var options = BuildOptions();
        var created = KcAppTestKit.SR_BaseInstant;
        var activeKid = await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created + Duration.FromHours(2));
        var retiringKid = await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Retiring,
            created,
            activatedAt: created,
            retiringAt: created + Duration.FromHours(3));

        var provider = MakeProvider(db, options);

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeTrue();
        result.Data!.Keys.Keys.Should().BeEquivalentTo([activeKid, retiringKid]);
    }

    [Fact]
    public async Task GetKeysAsync_EmptyStore_ReturnsServiceUnavailable()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var provider = MakeProvider(db, BuildOptions());

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetKeysAsync_NonSigningDomainKeys_Ignored_ReturnsUnavailable()
    {
        // Adversarial: AES payload keys must never appear as JWT verify keys.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var options = BuildOptions();
        var created = KcAppTestKit.SR_BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "audit",
            KeyType.AesPayload,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var provider = MakeProvider(db, options);

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetKeysAsync_DbThrows_ReturnsServiceUnavailable()
    {
        var provider = new InProcessJwksProvider(
            new FixedScopeFactory(new ThrowingDbContext()),
            Options.Create(BuildOptions()),
            NullLogger<InProcessJwksProvider>.Instance,
            TimeProvider.System);

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetKeysAsync_CorruptSpkiOnly_ReturnsServiceUnavailable()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        SeedCorruptSigningRow(db, "kid-corrupt-only");

        var provider = MakeProvider(db, BuildOptions());

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetKeysAsync_CorruptSpkiSkipped_KeepsValidKey()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var options = BuildOptions();
        var created = KcAppTestKit.SR_BaseInstant;
        var goodKid = await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created);
        SeedCorruptSigningRow(db, "kid-corrupt-skip");

        var provider = MakeProvider(db, options);

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeTrue();
        result.Data!.Keys.Should().ContainKey(goodKid);
        result.Data.Keys.Should().NotContainKey("kid-corrupt-skip");
        result.Data.Keys.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetKeysAsync_ConcurrentCalls_ShareSnapshot()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var options = BuildOptions();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var provider = MakeProvider(db, options);

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => provider.GetKeysAsync().AsTask())
            .ToArray();
        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r.Success);
        results.Select(r => r.Data!.Keys.Keys.Single()).Should().OnlyContain(k => k == kid);
        results.Select(r => r.Data).Distinct().Should().HaveCount(
            1,
            "singleflight + volatile snapshot publish one shared JwksKeySetSnapshot");
    }

    [Fact]
    public async Task GetKeysAsync_CanceledToken_ThrowsOperationCanceled()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var options = BuildOptions();
        var created = KcAppTestKit.SR_BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var provider = MakeProvider(db, options);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Capture token (struct) — not the disposable CTS — for the assertion lambda.
        var canceled = cts.Token;
        var act = async () => await provider.GetKeysAsync(canceled);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RefreshAsync_AfterNewKeySeeded_UpdatesSnapshot()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var options = BuildOptions();
        var created = KcAppTestKit.SR_BaseInstant;
        var firstKid = await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created);

        // Cooldown must not suppress the second refresh in this test — advance clock.
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var provider = MakeProvider(db, options, clock);

        var first = await provider.GetKeysAsync();
        first.Data!.Keys.Should().ContainKey(firstKid);

        var secondKid = await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created + Duration.FromHours(1),
            activatedAt: created + Duration.FromHours(1));

        clock.Advance(TimeSpan.FromSeconds(45));
        var refresh = await provider.RefreshAsync();
        refresh.Success.Should().BeTrue();

        var second = await provider.GetKeysAsync();
        second.Data!.Keys.Should().ContainKey(secondKid);
        second.Data.Keys.Should().ContainKey(firstKid);
    }

    [Fact]
    public async Task RefreshAsync_WithinCooldown_DoesNotReload()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var options = BuildOptions();
        var created = KcAppTestKit.SR_BaseInstant;
        var firstKid = await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var provider = MakeProvider(db, options, clock);

        await provider.GetKeysAsync();

        // Seed a second key but stay inside 30s cooldown — snapshot must stay stale.
        await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created + Duration.FromHours(1),
            activatedAt: created + Duration.FromHours(1));

        clock.Advance(TimeSpan.FromSeconds(5));
        var refresh = await provider.RefreshAsync();
        refresh.Success.Should().BeTrue();

        var snap = await provider.GetKeysAsync();
        snap.Data!.Keys.Should().ContainKey(firstKid);
        snap.Data.Keys.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_NullScopeFactory_Throws()
    {
        var act = () => new InProcessJwksProvider(
            scopeFactory: null!,
            options: Options.Create(BuildOptions()),
            logger: NullLogger<InProcessJwksProvider>.Instance,
            clock: TimeProvider.System);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton(KeyCustodianTestDbContext.CreateEmpty());
        var sp = services.BuildServiceProvider();

        var act = () => new InProcessJwksProvider(
            scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
            options: null!,
            logger: NullLogger<InProcessJwksProvider>.Instance,
            clock: TimeProvider.System);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton(KeyCustodianTestDbContext.CreateEmpty());
        var sp = services.BuildServiceProvider();

        var act = () => new InProcessJwksProvider(
            scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
            options: Options.Create(BuildOptions()),
            logger: null!,
            clock: TimeProvider.System);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullClock_Throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton(KeyCustodianTestDbContext.CreateEmpty());
        var sp = services.BuildServiceProvider();

        var act = () => new InProcessJwksProvider(
            scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
            options: Options.Create(BuildOptions()),
            logger: NullLogger<InProcessJwksProvider>.Instance,
            clock: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static KeyCustodianOptions BuildOptions()
    {
        var options = KcAppTestKit.BuildOptions();
        options.IssuerBaseUrl = _ISSUER;

        return options;
    }

    private static void SeedCorruptSigningRow(KeyCustodianTestDbContext db, string kid)
    {
        db.Keys.Add(new KeyRecord
        {
            Kid = kid,
            KeyDomain = "jwks-signing",
            KeyType = KeyType.RsaSigning,
            KeyMaterialEncrypted = [1, 2, 3],
            PublicKeyMaterial = [0x00, 0x01, 0x02, 0xFF],
            CreatedAt = KcAppTestKit.SR_BaseInstant,
            Status = KeyStatus.Active,
            ActivatedAt = KcAppTestKit.SR_BaseInstant,
        });
        db.SaveChanges();
    }

    private static InProcessJwksProvider MakeProvider(
        IKeyCustodianDbContext db,
        KeyCustodianOptions options,
        TimeProvider? clock = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        var sp = services.BuildServiceProvider();

        return new InProcessJwksProvider(
            new FixedScopeFactory(sp),
            Options.Create(options),
            NullLogger<InProcessJwksProvider>.Instance,
            clock ?? TimeProvider.System);
    }

    /// <summary>
    /// Minimal <see cref="IServiceScopeFactory"/> that always returns the same
    /// provider (in-memory test DbContext is process-owned, not real scoped).
    /// </summary>
    private sealed class FixedScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider r_sp;

        public FixedScopeFactory(IServiceProvider sp) => r_sp = sp;

        public FixedScopeFactory(IKeyCustodianDbContext db)
        {
            var services = new ServiceCollection();
            services.AddSingleton(db);
            r_sp = services.BuildServiceProvider();
        }

        public IServiceScope CreateScope() => new FixedScope(r_sp);

        private sealed class FixedScope(IServiceProvider sp) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = sp;

            public void Dispose()
            {
            }
        }
    }

    /// <summary>
    /// DbContext double whose <see cref="IKeyCustodianDbContext.Keys"/> access
    /// throws — models DB unreachable / provider failure for fail-secure SU.
    /// </summary>
    private sealed class ThrowingDbContext : IKeyCustodianDbContext
    {
        public DbSet<KeyRecord> Keys =>
            throw new InvalidOperationException("database unreachable");

        public DbSet<KeyAuditRecord> Audit =>
            throw new InvalidOperationException("database unreachable");

        public DbSet<LeafIssuanceAuditRecord> LeafIssuanceAudit =>
            throw new InvalidOperationException("database unreachable");

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database unreachable");

        public void ClearChangeTracker() =>
            throw new InvalidOperationException("database unreachable");
    }
}
