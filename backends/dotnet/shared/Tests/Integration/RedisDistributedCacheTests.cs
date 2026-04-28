// -----------------------------------------------------------------------
// <copyright file="RedisDistributedCacheTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable RedundantCapturedContext
// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable UnusedMember.Local
namespace D2.Shared.Tests.Integration;

using D2.Services.Protos.Geo.V1;

// ReSharper disable AccessToStaticMemberViaDerivedType
using D2.Shared.DistributedCache.Redis.Handlers.C;
using D2.Shared.DistributedCache.Redis.Handlers.D;
using D2.Shared.DistributedCache.Redis.Handlers.R;
using D2.Shared.DistributedCache.Redis.Handlers.U;
using D2.Shared.Handler;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.C;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.D;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.R;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.U;
using D2.Shared.Result;
using FluentAssertions;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using JetBrains.Annotations;
using StackExchange.Redis;
using Testcontainers.Redis;

/// <summary>
/// Integration tests for the Redis distributed cache service.
/// </summary>
[MustDisposeResource(false)]
public class RedisDistributedCacheTests : IAsyncLifetime
{
    private RedisContainer _container = null!;
    private IConnectionMultiplexer _redis = null!;
    private IHandlerContext _context = null!;
    private bool _containerStopped;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        _container = new RedisBuilder("redis:8.2").Build();
        await _container.StartAsync(Ct);

        _redis = await ConnectionMultiplexer.ConnectAsync(
            _container.GetConnectionString());
        _context = TestHelpers.CreateHandlerContext();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _redis.Dispose();
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Tests that getting a missing key returns NotFound.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Get_WhenKeyMissing_ReturnsNotFound()
    {
        var handler = new Get<string>(_redis, _context);
        var result = await handler.HandleAsync(
            new IRead.GetInput("missing-key"),
            Ct);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests that getting an existing key returns the correct value.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Get_WhenKeyExists_ReturnsValue()
    {
        var setHandler = new Set<string>(_redis, _context);
        await setHandler.HandleAsync(
            new IUpdate.SetInput<string>("test-key", "test-value"), Ct);

        var getHandler = new Get<string>(_redis, _context);
        var result = await getHandler.HandleAsync(
            new IRead.GetInput("test-key"), Ct);

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("test-value");
    }

    /// <summary>
    /// Tests that setting a value stores it in the cache.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Set_WithValidInput_StoresValueInCache()
    {
        var setHandler = new Set<string>(_redis, _context);
        var setResult = await setHandler.HandleAsync(
            new IUpdate.SetInput<string>("key", "value"), Ct);

        setResult.Success.Should().BeTrue();

        var db = _redis.GetDatabase();
        var cached = await db.StringGetAsync("key");
        cached.HasValue.Should().BeTrue();
    }

    /// <summary>
    /// Tests that setting a value with expiration expires after the specified duration.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Set_WithExpiration_ExpiresAfterDuration()
    {
        var handler = new Set<string>(_redis, _context);
        await handler.HandleAsync(
            new IUpdate.SetInput<string>("key", "value", TimeSpan.FromMilliseconds(100)), Ct);

        await Task.Delay(200, Ct);

        var db = _redis.GetDatabase();
        var cached = await db.StringGetAsync("key");
        cached.HasValue.Should().BeFalse();
    }

    /// <summary>
    /// Tests that removing an existing key deletes it from the cache.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Remove_WithExistingKey_DeletesKeyFromCache()
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync("key", "value");

        var handler = new Remove(_redis, _context);
        await handler.HandleAsync(new IDelete.RemoveInput("key"), Ct);

        var cached = await db.StringGetAsync("key");
        cached.HasValue.Should().BeFalse();
    }

    /// <summary>
    /// Tests that checking existence of an existing key returns true.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Exists_WhenKeyExists_ReturnsTrue()
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync("key", "value");

        var handler = new Exists(_redis, _context);
        var result = await handler.HandleAsync(new IRead.ExistsInput("key"), Ct);

        result.Success.Should().BeTrue();
        result.Data!.Exists.Should().BeTrue();
    }

    /// <summary>
    /// Tests that checking existence of a missing key returns false.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Exists_WhenKeyMissing_ReturnsFalse()
    {
        var handler = new Exists(_redis, _context);
        var result = await handler.HandleAsync(new IRead.ExistsInput("missing-key"), Ct);

        result.Success.Should().BeTrue();
        result.Data!.Exists.Should().BeFalse();
    }

    /// <summary>
    /// Tests that setting and getting a complex object serializes and deserializes correctly.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Set_WithComplexObject_SerializesAndDeserializesCorrectly()
    {
        var testObject = new TestData { Id = 42, Name = "Test" };

        var setHandler = new Set<TestData>(_redis, _context);
        await setHandler.HandleAsync(
            new IUpdate.SetInput<TestData>("complex-key", testObject),
            Ct);

        var getHandler = new Get<TestData>(_redis, _context);
        var result = await getHandler.HandleAsync(new IRead.GetInput("complex-key"), Ct);

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().BeEquivalentTo(testObject);
    }

    /// <summary>
    /// Tests that setting a protobuf message stores it correctly using binary serialization.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Set_WithProtobufMessage_StoresAsBinaryData()
    {
        // Arrange
        var protoData = TestHelpers.TestGeoRefData;
        var handler = new Set<GeoRefData>(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new IUpdate.SetInput<GeoRefData>("proto-key", protoData),
            Ct);

        // Assert
        result.Success.Should().BeTrue();

        var db = _redis.GetDatabase();
        var cached = await db.StringGetAsync("proto-key");
        cached.HasValue.Should().BeTrue();

        // Verify it's binary protobuf, not JSON (protobuf won't start with '{')
        var bytes = (byte[])cached!;
        bytes.Should().NotBeEmpty();
        bytes[0].Should().NotBe((byte)'{');
    }

    /// <summary>
    /// Tests that getting a protobuf message deserializes correctly using ParseProtobuf internally.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Get_WithProtobufMessage_DeserializesCorrectly()
    {
        // Arrange
        var protoData = TestHelpers.TestGeoRefData;

        var setHandler = new Set<GeoRefData>(_redis, _context);
        await setHandler.HandleAsync(
            new IUpdate.SetInput<GeoRefData>("proto-get-key", protoData),
            Ct);

        var getHandler = new Get<GeoRefData>(_redis, _context);

        // Act
        var result = await getHandler.HandleAsync(new IRead.GetInput("proto-get-key"), Ct);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();

        var retrieved = result.Data!.Value;
        retrieved!.Version.Should().Be(protoData.Version);
        retrieved.Countries.Should().ContainKey("US");
        retrieved.Countries["US"].DisplayName.Should().Be("United States");
        retrieved.Subdivisions.Should().ContainKey("US-AL");
        retrieved.Subdivisions["US-AL"].DisplayName.Should().Be("Alabama");
        retrieved.Currencies.Should().ContainKey("USD");
        retrieved.Currencies["USD"].Symbol.Should().Be("$");
        retrieved.Languages.Should().ContainKey("en");
        retrieved.Locales.Should().ContainKey("en-US");
        retrieved.GeopoliticalEntities.Should().ContainKey("NATO");
    }

    /// <summary>
    /// Tests that getting a value with corrupted JSON data returns deserialization error.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Get_WhenDataIsCorruptedJson_ReturnsDeserializationError()
    {
        // Arrange - store malformed JSON directly in Redis
        var db = _redis.GetDatabase();
        await db.StringSetAsync("corrupted-key", "{invalid json that wont parse}}}");

        var handler = new Get<TestData>(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new IRead.GetInput("corrupted-key"),
            Ct);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(ErrorCodes.UNHANDLED_EXCEPTION);
    }

    /// <summary>
    /// Tests that getting a protobuf type without a Parser property returns failure.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Get_WithFakeMessageWithoutParser_ReturnsFail()
    {
        // Arrange - store some binary data that looks like protobuf (non-printable first byte)
        var db = _redis.GetDatabase();
        await db.StringSetAsync("fake-proto-key", new byte[] { 0x0A, 0x01, 0x02, 0x03 });

        var handler = new Get<FakeMessageWithoutParser>(_redis, _context);

        // Act
        var result = await handler.HandleAsync(new IRead.GetInput("fake-proto-key"), Ct);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(ErrorCodes.UNHANDLED_EXCEPTION);
    }

    /// <summary>
    /// Tests that getting a protobuf type with null Parser returns failure.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Get_WithFakeMessageWithNullParser_ReturnsFail()
    {
        // Arrange - store some binary data that looks like protobuf (non-printable first byte)
        var db = _redis.GetDatabase();
        await db.StringSetAsync("null-parser-key", new byte[] { 0x0A, 0x01, 0x02, 0x03 });

        var handler = new Get<FakeMessageWithNullParser>(_redis, _context);

        // Act
        var result = await handler.HandleAsync(new IRead.GetInput("null-parser-key"), Ct);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(ErrorCodes.UNHANDLED_EXCEPTION);
    }

    /// <summary>
    /// Tests that getting a protobuf type without ParseFrom method returns failure.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Get_WithFakeMessageWithoutParseFrom_ReturnsFail()
    {
        // Arrange - store some binary data that looks like protobuf (non-printable first byte)
        var db = _redis.GetDatabase();
        await db.StringSetAsync("no-parsefrom-key", new byte[] { 0x0A, 0x01, 0x02, 0x03 });

        var handler = new Get<FakeMessageWithoutParseFrom>(_redis, _context);

        // Act
        var result = await handler.HandleAsync(new IRead.GetInput("no-parsefrom-key"), Ct);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(ErrorCodes.UNHANDLED_EXCEPTION);
    }

    /// <summary>
    /// Tests that getting a value when Redis is unavailable returns ServiceUnavailable.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Get_WhenRedisUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        await EnsureContainerStoppedAsync();
        var handler = new Get<string>(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new IRead.GetInput("any-key"),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    /// <summary>
    /// Tests that setting a value when Redis is unavailable returns ServiceUnavailable.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Set_WhenRedisUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        await EnsureContainerStoppedAsync();
        var handler = new Set<string>(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new IUpdate.SetInput<string>("any-key", "any-value"),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    /// <summary>
    /// Tests that checking existence when Redis is unavailable returns ServiceUnavailable.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Exists_WhenRedisUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        await EnsureContainerStoppedAsync();
        var handler = new Exists(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new IRead.ExistsInput("any-key"),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    /// <summary>
    /// Tests that removing a key when Redis is unavailable returns ServiceUnavailable.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Remove_WhenRedisUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        await EnsureContainerStoppedAsync();
        var handler = new Remove(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new IDelete.RemoveInput("any-key"),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    // ── SetNx ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Tests that SetNx sets the value and returns true when the key does not exist.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SetNx_WhenKeyDoesNotExist_SetsAndReturnsTrue()
    {
        var handler = new SetNx<string>(_redis, _context);

        var result = await handler.HandleAsync(
            new ICreate.SetNxInput<string>("nx-new-key", "first-value"),
            Ct);

        result.Success.Should().BeTrue();
        result.Data!.WasSet.Should().BeTrue();

        var db = _redis.GetDatabase();
        var cached = await db.StringGetAsync("nx-new-key");
        cached.HasValue.Should().BeTrue();
    }

    /// <summary>
    /// Tests that SetNx does not overwrite an existing key and returns false.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SetNx_WhenKeyExists_DoesNotOverwriteAndReturnsFalse()
    {
        // Arrange — set the key first
        var handler = new SetNx<string>(_redis, _context);
        await handler.HandleAsync(
            new ICreate.SetNxInput<string>("nx-existing-key", "original"),
            Ct);

        // Act — attempt to set again with a different value
        var result = await handler.HandleAsync(
            new ICreate.SetNxInput<string>("nx-existing-key", "overwrite-attempt"),
            Ct);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.WasSet.Should().BeFalse();

        // Verify original value is preserved
        var getHandler = new Get<string>(_redis, _context);
        var getResult = await getHandler.HandleAsync(
            new IRead.GetInput("nx-existing-key"), Ct);
        getResult.Data!.Value.Should().Be("original");
    }

    /// <summary>
    /// Tests that SetNx with expiration causes the key to expire after the duration.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SetNx_WithExpiration_KeyExpiresAfterDuration()
    {
        var handler = new SetNx<string>(_redis, _context);
        await handler.HandleAsync(
            new ICreate.SetNxInput<string>(
                "nx-expiring-key", "temp-value", TimeSpan.FromMilliseconds(100)),
            Ct);

        await Task.Delay(200, Ct);

        var db = _redis.GetDatabase();
        var cached = await db.StringGetAsync("nx-expiring-key");
        cached.HasValue.Should().BeFalse();
    }

    /// <summary>
    /// Tests that SetNx with a protobuf message stores it as binary data.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SetNx_WithProtobufMessage_StoresAsBinary()
    {
        // Arrange
        var protoData = TestHelpers.TestGeoRefData;
        var handler = new SetNx<GeoRefData>(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new ICreate.SetNxInput<GeoRefData>("nx-proto-key", protoData),
            Ct);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.WasSet.Should().BeTrue();

        var db = _redis.GetDatabase();
        var cached = await db.StringGetAsync("nx-proto-key");
        cached.HasValue.Should().BeTrue();

        // Verify it's binary protobuf, not JSON (protobuf won't start with '{')
        var bytes = (byte[])cached!;
        bytes.Should().NotBeEmpty();
        bytes[0].Should().NotBe((byte)'{');
    }

    /// <summary>
    /// Tests that SetNx returns ServiceUnavailable when Redis is unavailable.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SetNx_WhenRedisUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        await EnsureContainerStoppedAsync();
        var handler = new SetNx<string>(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new ICreate.SetNxInput<string>("any-key", "any-value"),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    // ── AcquireLock ──────────────────────────────────────────────────────────

    /// <summary>
    /// Tests that AcquireLock acquires when the key does not exist.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task AcquireLock_WhenKeyDoesNotExist_AcquiresAndReturnsTrue()
    {
        var handler = new AcquireLock(_redis, _context);

        var result = await handler.HandleAsync(
            new ICreate.AcquireLockInput("lock:test", "owner-1", TimeSpan.FromMinutes(1)),
            Ct);

        result.Success.Should().BeTrue();
        result.Data!.Acquired.Should().BeTrue();

        var db = _redis.GetDatabase();
        var stored = await db.StringGetAsync("lock:test");
        stored.ToString().Should().Be("owner-1");
    }

    /// <summary>
    /// Tests that AcquireLock does not overwrite when the key is already held.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task AcquireLock_WhenKeyAlreadyHeld_DoesNotOverwriteAndReturnsFalse()
    {
        var handler = new AcquireLock(_redis, _context);

        await handler.HandleAsync(
            new ICreate.AcquireLockInput("lock:contest", "owner-1", TimeSpan.FromMinutes(1)),
            Ct);

        var result = await handler.HandleAsync(
            new ICreate.AcquireLockInput("lock:contest", "owner-2", TimeSpan.FromMinutes(1)),
            Ct);

        result.Success.Should().BeTrue();
        result.Data!.Acquired.Should().BeFalse();

        var db = _redis.GetDatabase();
        var stored = await db.StringGetAsync("lock:contest");
        stored.ToString().Should().Be("owner-1");
    }

    /// <summary>
    /// Tests that AcquireLock sets the expected TTL on the key.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task AcquireLock_SetsExpiration()
    {
        var handler = new AcquireLock(_redis, _context);

        await handler.HandleAsync(
            new ICreate.AcquireLockInput("lock:ttl-test", "owner-1", TimeSpan.FromSeconds(30)),
            Ct);

        var db = _redis.GetDatabase();
        var ttl = await db.KeyTimeToLiveAsync("lock:ttl-test");
        ttl.Should().NotBeNull();
        ttl.Value.Should().BeGreaterThan(TimeSpan.Zero);
        ttl.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Tests that AcquireLock returns ServiceUnavailable when Redis is unavailable.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task AcquireLock_WhenRedisUnavailable_ReturnsServiceUnavailable()
    {
        await EnsureContainerStoppedAsync();
        var handler = new AcquireLock(_redis, _context);

        var result = await handler.HandleAsync(
            new ICreate.AcquireLockInput("any-key", "any-id", TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    // ── ReleaseLock ─────────────────────────────────────────────────────────

    /// <summary>
    /// Tests that ReleaseLock releases when the lockId matches.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ReleaseLock_WhenLockIdMatches_ReleasesAndReturnsTrue()
    {
        var acquireHandler = new AcquireLock(_redis, _context);
        await acquireHandler.HandleAsync(
            new ICreate.AcquireLockInput("lock:release-test", "owner-1", TimeSpan.FromMinutes(1)),
            Ct);

        var handler = new ReleaseLock(_redis, _context);
        var result = await handler.HandleAsync(
            new IDelete.ReleaseLockInput("lock:release-test", "owner-1"),
            Ct);

        result.Success.Should().BeTrue();
        result.Data!.Released.Should().BeTrue();

        var db = _redis.GetDatabase();
        var stored = await db.StringGetAsync("lock:release-test");
        stored.HasValue.Should().BeFalse();
    }

    /// <summary>
    /// Tests that ReleaseLock does not release when the lockId does not match.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ReleaseLock_WhenLockIdDoesNotMatch_DoesNotReleaseAndReturnsFalse()
    {
        var acquireHandler = new AcquireLock(_redis, _context);
        await acquireHandler.HandleAsync(
            new ICreate.AcquireLockInput("lock:mismatch-test", "owner-1", TimeSpan.FromMinutes(1)),
            Ct);

        var handler = new ReleaseLock(_redis, _context);
        var result = await handler.HandleAsync(
            new IDelete.ReleaseLockInput("lock:mismatch-test", "owner-2"),
            Ct);

        result.Success.Should().BeTrue();
        result.Data!.Released.Should().BeFalse();

        var db = _redis.GetDatabase();
        var stored = await db.StringGetAsync("lock:mismatch-test");
        stored.ToString().Should().Be("owner-1");
    }

    /// <summary>
    /// Tests that ReleaseLock returns false when the key does not exist.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ReleaseLock_WhenKeyDoesNotExist_ReturnsFalse()
    {
        var handler = new ReleaseLock(_redis, _context);
        var result = await handler.HandleAsync(
            new IDelete.ReleaseLockInput("lock:nonexistent", "owner-1"),
            Ct);

        result.Success.Should().BeTrue();
        result.Data!.Released.Should().BeFalse();
    }

    /// <summary>
    /// Tests that ReleaseLock returns ServiceUnavailable when Redis is unavailable.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ReleaseLock_WhenRedisUnavailable_ReturnsServiceUnavailable()
    {
        await EnsureContainerStoppedAsync();
        var handler = new ReleaseLock(_redis, _context);

        var result = await handler.HandleAsync(
            new IDelete.ReleaseLockInput("any-key", "any-id"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    // ── GetTtl ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Tests that GetTtl returns the remaining TTL when a key has an expiration set.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task GetTtl_WhenKeyHasTtl_ReturnsTtl()
    {
        // Arrange — set a key with a known TTL
        var db = _redis.GetDatabase();
        await db.StringSetAsync("ttl-key", "value", TimeSpan.FromSeconds(30));

        var handler = new GetTtl(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new IRead.GetTtlInput("ttl-key"), Ct);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.TimeToLive.Should().NotBeNull();
        result.Data!.TimeToLive!.Value.Should().BeGreaterThan(TimeSpan.Zero);
        result.Data!.TimeToLive!.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Tests that GetTtl returns null when a key has no expiration (persistent).
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task GetTtl_WhenKeyHasNoTtl_ReturnsNull()
    {
        // Arrange — set a key without expiration
        var db = _redis.GetDatabase();
        await db.StringSetAsync("persistent-key", "value");

        var handler = new GetTtl(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new IRead.GetTtlInput("persistent-key"), Ct);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.TimeToLive.Should().BeNull();
    }

    /// <summary>
    /// Tests that GetTtl returns null when the key does not exist.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task GetTtl_WhenKeyMissing_ReturnsNull()
    {
        var handler = new GetTtl(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new IRead.GetTtlInput("nonexistent-key"), Ct);

        // Assert — Redis returns -2 for missing keys, StackExchange maps to null
        result.Success.Should().BeTrue();
        result.Data!.TimeToLive.Should().BeNull();
    }

    /// <summary>
    /// Tests that GetTtl returns ServiceUnavailable when Redis is unavailable.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task GetTtl_WhenRedisUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        await EnsureContainerStoppedAsync();
        var handler = new GetTtl(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new IRead.GetTtlInput("any-key"),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    // ── Increment ──────────────────────────────────────────────────────────

    /// <summary>
    /// Tests that incrementing a non-existent key returns 1 (Redis auto-creates with value 0 then increments).
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Increment_NewKey_ReturnsOne()
    {
        var handler = new Increment(_redis, _context);

        var result = await handler.HandleAsync(
            new IUpdate.IncrementInput("incr-new-key"),
            Ct);

        result.Success.Should().BeTrue();
        result.Data!.NewValue.Should().Be(1);
    }

    /// <summary>
    /// Tests that incrementing an existing numeric key returns the correctly incremented value.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Increment_ExistingKey_ReturnsIncrementedValue()
    {
        // Arrange — set the key to "5" (Redis stores numbers as strings)
        var db = _redis.GetDatabase();
        await db.StringSetAsync("incr-existing-key", "5");

        var handler = new Increment(_redis, _context);

        // Act — increment by 3
        var result = await handler.HandleAsync(
            new IUpdate.IncrementInput("incr-existing-key", 3),
            Ct);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.NewValue.Should().Be(8);
    }

    /// <summary>
    /// Tests that incrementing with an expiration sets a TTL on the key.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Increment_WithExpiration_SetsKeyTtl()
    {
        var handler = new Increment(_redis, _context);

        // Act — increment with a 30s expiration
        var result = await handler.HandleAsync(
            new IUpdate.IncrementInput("incr-ttl-key", 1, TimeSpan.FromSeconds(30)),
            Ct);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.NewValue.Should().Be(1);

        var db = _redis.GetDatabase();
        var ttl = await db.KeyTimeToLiveAsync("incr-ttl-key");
        ttl.Should().NotBeNull();
        ttl.Value.Should().BeGreaterThan(TimeSpan.Zero);
        ttl.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Tests that Increment returns ServiceUnavailable when Redis is unavailable.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the result of the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Increment_WhenRedisUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        await EnsureContainerStoppedAsync();
        var handler = new Increment(_redis, _context);

        // Act
        var result = await handler.HandleAsync(
            new IUpdate.IncrementInput("any-key"),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    /// <summary>
    /// Ensures the Redis container is stopped for unavailability tests.
    /// Safe to call multiple times.
    /// </summary>
    private async Task EnsureContainerStoppedAsync()
    {
        if (_containerStopped)
        {
            return;
        }

        await _container.StopAsync(Ct);
        _containerStopped = true;
    }

    /// <summary>
    /// Fake IMessage implementation without a Parser property.
    /// </summary>
    private sealed class FakeMessageWithoutParser : IMessage
    {
        /// <inheritdoc/>
        public MessageDescriptor Descriptor => null!;

        /// <inheritdoc/>
        public void MergeFrom(CodedInputStream input)
        {
        }

        /// <inheritdoc/>
        public void WriteTo(CodedOutputStream output)
        {
        }

        /// <inheritdoc/>
        public int CalculateSize() => 0;
    }

    /// <summary>
    /// Fake IMessage implementation with a null Parser property.
    /// </summary>
    private sealed class FakeMessageWithNullParser : IMessage
    {
        /// <summary>
        /// Gets the parser (always null for testing).
        /// </summary>
        public static object? Parser => null;

        /// <inheritdoc/>
        public MessageDescriptor Descriptor => null!;

        /// <inheritdoc/>
        public void MergeFrom(CodedInputStream input)
        {
        }

        /// <inheritdoc/>
        public void WriteTo(CodedOutputStream output)
        {
        }

        /// <inheritdoc/>
        public int CalculateSize() => 0;
    }

    /// <summary>
    /// Fake IMessage implementation with a Parser that lacks ParseFrom method.
    /// </summary>
    private sealed class FakeMessageWithoutParseFrom : IMessage
    {
        /// <summary>
        /// Gets the parser (missing ParseFrom method).
        /// </summary>
        public static FakeParser Parser => new();

        /// <inheritdoc/>
        public MessageDescriptor Descriptor => null!;

        /// <inheritdoc/>
        public void MergeFrom(CodedInputStream input)
        {
        }

        /// <inheritdoc/>
        public void WriteTo(CodedOutputStream output)
        {
        }

        /// <inheritdoc/>
        public int CalculateSize() => 0;

        /// <summary>
        /// Fake parser without ParseFrom method.
        /// </summary>
        public sealed class FakeParser
        {
            // Intentionally missing ParseFrom(byte[]) method
        }
    }

    /// <summary>
    /// Test data class for complex object caching tests.
    /// </summary>
    private sealed record TestData
    {
        /// <summary>
        /// Gets the identifier.
        /// </summary>
        [UsedImplicitly]
        public int Id { get; init; }

        /// <summary>
        /// Gets the name.
        /// </summary>
        [UsedImplicitly]
        public string Name { get; init; } = string.Empty;
    }
}
