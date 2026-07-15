// -----------------------------------------------------------------------
// <copyright file="D2ResultAsyncExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using Xunit;

public sealed class D2ResultAsyncExtensionsTests
{
    // ----------------------------------------------------------------------
    // BindAsync — ValueTask
    // ----------------------------------------------------------------------

    [Fact]
    public async Task BindAsync_ValueTask_OnOk_ChainsToNext()
    {
        ValueTask<D2Result<int>> upstream = ValueTask.FromResult(D2Result<int>.Ok(5));

        var result = await upstream.BindAsync(x =>
            ValueTask.FromResult(D2Result<string>.Ok($"v={x}")));

        result.Success.Should().BeTrue();
        result.Data.Should().Be("v=5");
    }

    [Fact]
    public async Task BindAsync_ValueTask_OnFailure_ShortCircuits()
    {
        ValueTask<D2Result<int>> upstream = ValueTask.FromResult(D2Result<int>.NotFound());
        var nextInvoked = false;

        var result = await upstream.BindAsync(x =>
        {
            nextInvoked = true;
            return ValueTask.FromResult(D2Result<string>.Ok($"v={x}"));
        });

        nextInvoked.Should().BeFalse();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NOT_FOUND);
    }

    [Fact]
    public async Task BindAsync_ValueTask_NextReturnsFailure_PropagatesNext()
    {
        ValueTask<D2Result<int>> upstream = ValueTask.FromResult(D2Result<int>.Ok(5));

        var result = await upstream.BindAsync(_ =>
            ValueTask.FromResult(D2Result<string>.Forbidden(messages: [TK.Common.Errors.UNKNOWN])));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.FORBIDDEN);
        result.Messages.Should().Equal(TK.Common.Errors.UNKNOWN);
    }

    // ----------------------------------------------------------------------
    // BindAsync — Task
    // ----------------------------------------------------------------------

    [Fact]
    public async Task BindAsync_Task_OnOk_ChainsToNext()
    {
        Task<D2Result<int>> upstream = Task.FromResult(D2Result<int>.Ok(5));

        var result = await upstream.BindAsync(x =>
            Task.FromResult(D2Result<string>.Ok($"v={x}")));

        result.Success.Should().BeTrue();
        result.Data.Should().Be("v=5");
    }

    [Fact]
    public async Task BindAsync_Task_OnFailure_ShortCircuits()
    {
        Task<D2Result<int>> upstream = Task.FromResult(D2Result<int>.Conflict());
        var nextInvoked = false;

        var result = await upstream.BindAsync(x =>
        {
            nextInvoked = true;
            return Task.FromResult(D2Result<string>.Ok($"v={x}"));
        });

        nextInvoked.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CONFLICT);
    }

    // ----------------------------------------------------------------------
    // MapAsync — ValueTask
    // ----------------------------------------------------------------------

    [Fact]
    public async Task MapAsync_ValueTask_OnOk_AppliesProjection()
    {
        ValueTask<D2Result<int>> upstream = ValueTask.FromResult(D2Result<int>.Ok(10));

        var result = await upstream.MapAsync(x => x * 3);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(30);
    }

    [Fact]
    public async Task MapAsync_ValueTask_OnFailure_DoesNotInvokeProjection()
    {
        ValueTask<D2Result<int>> upstream = ValueTask.FromResult(D2Result<int>.NotFound());
        var invocations = 0;

        var result = await upstream.MapAsync(x =>
        {
            invocations++;
            return x * 2;
        });

        invocations.Should().Be(0);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NOT_FOUND);
    }

    [Fact]
    public async Task MapAsync_ValueTask_TypeChange()
    {
        ValueTask<D2Result<int>> upstream = ValueTask.FromResult(D2Result<int>.Ok(7));

        var result = await upstream.MapAsync(x => $"n={x}");

        result.Data.Should().Be("n=7");
    }

    // ----------------------------------------------------------------------
    // MapAsync — Task
    // ----------------------------------------------------------------------

    [Fact]
    public async Task MapAsync_Task_OnOk_AppliesProjection()
    {
        Task<D2Result<int>> upstream = Task.FromResult(D2Result<int>.Ok(10));

        var result = await upstream.MapAsync(x => x * 3);

        result.Data.Should().Be(30);
    }

    [Fact]
    public async Task MapAsync_Task_OnFailure_DoesNotInvokeProjection()
    {
        Task<D2Result<int>> upstream = Task.FromResult(D2Result<int>.Forbidden());
        var invocations = 0;

        var result = await upstream.MapAsync(x =>
        {
            invocations++;
            return x;
        });

        invocations.Should().Be(0);
        result.ErrorCode.Should().Be(ErrorCodes.FORBIDDEN);
    }

    // ----------------------------------------------------------------------
    // ThenAsync — same-shape chain
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ThenAsync_ValueTask_OnOk_ChainsToNext()
    {
        ValueTask<D2Result<int>> upstream = ValueTask.FromResult(D2Result<int>.Ok(5));

        var result = await upstream.ThenAsync(x =>
            ValueTask.FromResult(D2Result<int>.Ok(x + 1)));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(6);
    }

    [Fact]
    public async Task ThenAsync_ValueTask_OnFailure_ShortCircuits()
    {
        ValueTask<D2Result<int>> upstream = ValueTask.FromResult(D2Result<int>.Canceled());
        var nextInvoked = false;

        var result = await upstream.ThenAsync(x =>
        {
            nextInvoked = true;
            return ValueTask.FromResult(D2Result<int>.Ok(x));
        });

        nextInvoked.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CANCELED);
    }

    [Fact]
    public async Task ThenAsync_Task_OnOk_ChainsToNext()
    {
        Task<D2Result<int>> upstream = Task.FromResult(D2Result<int>.Ok(5));

        var result = await upstream.ThenAsync(x =>
            Task.FromResult(D2Result<int>.Ok(x + 10)));

        result.Data.Should().Be(15);
    }

    [Fact]
    public async Task ThenAsync_Task_OnFailure_ShortCircuits()
    {
        Task<D2Result<int>> upstream = Task.FromResult(D2Result<int>.Unauthorized());

        var result = await upstream.ThenAsync(x => Task.FromResult(D2Result<int>.Ok(x)));

        result.ErrorCode.Should().Be(ErrorCodes.UNAUTHORIZED);
    }

    // ----------------------------------------------------------------------
    // Multi-step async chain — short-circuit propagation across mixed operators
    // ----------------------------------------------------------------------

    [Fact]
    public async Task BindAsync_Chain_ShortCircuitsOnMidFailure()
    {
        ValueTask<D2Result<int>> seed = ValueTask.FromResult(D2Result<int>.Ok(5));
        var step3Invoked = false;

        var result = await seed
            .BindAsync(x => ValueTask.FromResult(D2Result<int>.Ok(x + 1)))
            .BindAsync(_ => ValueTask.FromResult(
                D2Result<int>.NotFound(messages: [TK.Common.Errors.UNKNOWN])))
            .BindAsync(x =>
            {
                step3Invoked = true;
                return ValueTask.FromResult(D2Result<int>.Ok(x));
            });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NOT_FOUND);
        result.Messages.Should().Equal(TK.Common.Errors.UNKNOWN);
        step3Invoked.Should().BeFalse();
    }

    [Fact]
    public async Task MixedChain_BindMapBind_AllAwaitedOnSuccess()
    {
        ValueTask<D2Result<int>> seed = ValueTask.FromResult(D2Result<int>.Ok(2));

        var result = await seed
            .BindAsync(x => ValueTask.FromResult(D2Result<int>.Ok(x + 1)))
            .MapAsync(x => x * 10)
            .BindAsync(x => ValueTask.FromResult(D2Result<string>.Ok($"={x}")));

        result.Success.Should().BeTrue();
        result.Data.Should().Be("=30");
    }

    [Fact]
    public async Task BindAsync_NextThrows_ExceptionPropagates()
    {
        // Adversarial: async ext methods do NOT catch exceptions thrown from next.
        // Exceptional cases bubble out of the chain — D2Result.UnhandledException is
        // for KNOWN-classified failures, not for swallowing arbitrary throws here.
        ValueTask<D2Result<int>> upstream = ValueTask.FromResult(D2Result<int>.Ok(5));

        var act = async () => await upstream.BindAsync<int, string>(
            _ => throw new InvalidOperationException("boom"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }
}
