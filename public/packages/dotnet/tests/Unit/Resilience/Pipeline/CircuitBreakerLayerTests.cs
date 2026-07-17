// -----------------------------------------------------------------------
// <copyright file="CircuitBreakerLayerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.Pipeline;
using Xunit;

public sealed class CircuitBreakerLayerTests
{
    [Fact]
    public async Task WrapAsync_DelegatesToUnderlyingCircuitBreaker()
    {
        var cb = new CircuitBreaker<int>(_ => false);
        var layer = new CircuitBreakerLayer<string, int>(cb);

        var result = await layer.WrapAsync(
            "k", _ => ValueTask.FromResult(42), CancellationToken.None);

        result.Should().Be(42);
        cb.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task WrapAsync_OpenBreaker_ThrowsCircuitOpenException()
    {
        // Force the breaker open via a 1-failure threshold so the next call
        // through the layer fast-fails — proves CB.ExecuteAsync semantics
        // are preserved end-to-end.
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1));
        var layer = new CircuitBreakerLayer<string, int>(cb);

        try
        {
            await layer.WrapAsync(
                "k", _ => throw new InvalidOperationException(), CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // expected — opens the circuit
        }

        cb.State.Should().Be(CircuitState.Open);

        var act = async () => await layer.WrapAsync(
            "k", _ => ValueTask.FromResult(1), CancellationToken.None);

        await act.Should().ThrowAsync<CircuitOpenException>();
    }

    [Fact]
    public async Task WrapAsync_KeyArgument_IsIgnored()
    {
        // The CB layer ignores the per-call key — any value yields the same
        // behavior. Verify by passing two different keys; both succeed.
        var cb = new CircuitBreaker<int>(_ => false);
        var layer = new CircuitBreakerLayer<string, int>(cb);

        (await layer.WrapAsync("a", _ => ValueTask.FromResult(1), CancellationToken.None))
            .Should().Be(1);
        (await layer.WrapAsync("b", _ => ValueTask.FromResult(2), CancellationToken.None))
            .Should().Be(2);
    }
}
