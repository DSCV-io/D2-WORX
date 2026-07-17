// -----------------------------------------------------------------------
// <copyright file="D2ResultMonadicTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using Xunit;

public sealed class D2ResultMonadicTests
{
    // ----------------------------------------------------------------------
    // Bind
    // ----------------------------------------------------------------------

    [Fact]
    public void Bind_OnOk_InvokesNextWithData()
    {
        var seed = D2Result<int>.Ok(5);

        var result = seed.Bind(x => D2Result<string>.Ok($"v={x}"));

        result.Success.Should().BeTrue();
        result.Data.Should().Be("v=5");
    }

    [Fact]
    public void Bind_OnFailure_DoesNotInvokeNext()
    {
        // Lazy evaluation: next must NOT be called when upstream failed.
        var seed = D2Result<int>.NotFound();
        var invocations = 0;

        var result = seed.Bind(x =>
        {
            invocations++;
            return D2Result<string>.Ok($"v={x}");
        });

        invocations.Should().Be(0);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NOT_FOUND);
    }

    [Fact]
    public void Bind_OnFailure_PropagatesUpstreamMetadata()
    {
        var seed = D2Result<int>.Conflict(messages: [TK.Common.Errors.UNKNOWN], traceId: "t-up");

        var result = seed.Bind(_ => D2Result<string>.Ok("never"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CONFLICT);
        result.Messages.Should().Equal(TK.Common.Errors.UNKNOWN);
        result.TraceId.Should().Be("t-up");
    }

    [Fact]
    public void Bind_NextReturnsFailure_PropagatesNextFailureNotUpstream()
    {
        // Adversarial: when next returns its OWN failure, that failure propagates
        // (not the upstream success).
        var seed = D2Result<int>.Ok(5);

        var result = seed.Bind(_ =>
            D2Result<string>.Forbidden(messages: [TK.Common.Errors.UNKNOWN]));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.FORBIDDEN);
        result.Messages.Should().Equal(TK.Common.Errors.UNKNOWN);
    }

    [Fact]
    public void Bind_Chain_ShortCircuitsOnMidFailure()
    {
        // Three-step chain: Ok → Failure → never-called. Verify both that the failure
        // propagates AND that the third step is never invoked.
        var seed = D2Result<int>.Ok(5);
        var step3Invoked = false;

        var result = seed
            .Bind(_ => D2Result<int>.NotFound(messages: [TK.Common.Errors.UNKNOWN]))
            .Bind(x =>
            {
                step3Invoked = true;
                return D2Result<int>.Ok(x);
            });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NOT_FOUND);
        result.Messages.Should().Equal(TK.Common.Errors.UNKNOWN);
        step3Invoked.Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Map
    // ----------------------------------------------------------------------

    [Fact]
    public void Map_OnOk_AppliesProjection()
    {
        var seed = D2Result<int>.Ok(10);

        var result = seed.Map(x => x * 2);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(20);
    }

    [Fact]
    public void Map_TypeChange_ProjectionAcrossTypes()
    {
        var seed = D2Result<int>.Ok(7);

        var result = seed.Map(x => $"n={x}");

        result.Data.Should().Be("n=7");
    }

    [Fact]
    public void Map_OnFailure_DoesNotInvokeProjection()
    {
        var seed = D2Result<int>.Conflict();
        var invocations = 0;

        var result = seed.Map(x =>
        {
            invocations++;
            return x * 2;
        });

        invocations.Should().Be(0);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CONFLICT);
    }

    [Fact]
    public void Map_OnFailure_PropagatesUpstreamMetadata()
    {
        var seed = D2Result<int>.ValidationFailed(
            messages: [TK.Common.Errors.UNKNOWN],
            inputErrors: [new InputError("x", [TK.Common.Validation.NON_EMPTY_LIST])],
            traceId: "t");

        var result = seed.Map(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
        result.Messages.Should().Equal(TK.Common.Errors.UNKNOWN);
        result.InputErrors.Should().HaveCount(1);
        result.TraceId.Should().Be("t");
    }

    [Fact]
    public void Map_ProjectionThrows_ExceptionPropagates()
    {
        // Adversarial: Map does NOT catch exceptions — projection failures throw out
        // of the chain. This is intentional; D2Result wraps known failure modes via
        // factories, not exceptional cases.
        var seed = D2Result<int>.Ok(5);

        var act = () => seed.Map<string>(_ => throw new InvalidOperationException("boom"));

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    // ----------------------------------------------------------------------
    // Match
    // ----------------------------------------------------------------------

    [Fact]
    public void Match_OnOk_InvokesOnSuccessWithData()
    {
        var seed = D2Result<int>.Ok(42);

        var result = seed.Match(
            onSuccess: x => $"ok:{x}",
            onFailure: r => $"fail:{r.ErrorCode}");

        result.Should().Be("ok:42");
    }

    [Fact]
    public void Match_OnFailure_InvokesOnFailureWithFullResult()
    {
        var seed = D2Result<int>.NotFound(messages: [TK.Common.Errors.UNKNOWN]);

        var result = seed.Match(
            onSuccess: _ => "should not be called",
            onFailure: r => $"{r.ErrorCode}:{r.Messages.Count}");

        result.Should().Be($"{ErrorCodes.NOT_FOUND}:1");
    }

    [Fact]
    public void Match_OnSuccess_DoesNotInvokeOnFailureBranch()
    {
        var seed = D2Result<int>.Ok(1);
        var failureBranchInvocations = 0;

        seed.Match(
            onSuccess: x => x,
            onFailure: _ =>
            {
                failureBranchInvocations++;
                return -1;
            });

        failureBranchInvocations.Should().Be(0);
    }

    [Fact]
    public void Match_OnFailure_DoesNotInvokeOnSuccessBranch()
    {
        var seed = D2Result<int>.Conflict();
        var successBranchInvocations = 0;

        seed.Match(
            onSuccess: _ =>
            {
                successBranchInvocations++;
                return 1;
            },
            onFailure: _ => -1);

        successBranchInvocations.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // Monadic laws
    // ----------------------------------------------------------------------

    [Fact]
    public void MonadLaw_LeftIdentity_OkBindFEqualsFOfX()
    {
        // Left identity: Ok(x).Bind(f) ≡ f(x)
        const int x_value = 7;
        Func<int, D2Result<string>> f = i => D2Result<string>.Ok($"v={i}");

        var lhs = D2Result<int>.Ok(x_value).Bind(f);
        var rhs = f(x_value);

        lhs.Success.Should().Be(rhs.Success);
        lhs.Data.Should().Be(rhs.Data);
        lhs.StatusCode.Should().Be(rhs.StatusCode);
    }

    [Fact]
    public void MonadLaw_RightIdentity_MBindOkEqualsM()
    {
        // Right identity: m.Bind(Ok) ≡ m. Lambda used instead of method group
        // because D2Result<T>.Ok has all-optional params that defeat overload inference.
        var m = D2Result<int>.Ok(42);

        var rhs = m.Bind(x => D2Result<int>.Ok(x));

        rhs.Success.Should().Be(m.Success);
        rhs.Data.Should().Be(m.Data);
        rhs.StatusCode.Should().Be(m.StatusCode);
    }

    [Fact]
    public void MonadLaw_RightIdentity_HoldsForFailureToo()
    {
        // For failure, "Ok" is never invoked, but the failure propagates unchanged.
        var m = D2Result<int>.Conflict(messages: [TK.Common.Errors.UNKNOWN]);

        var rhs = m.Bind(x => D2Result<int>.Ok(x));

        rhs.Success.Should().BeFalse();
        rhs.ErrorCode.Should().Be(ErrorCodes.CONFLICT);
        rhs.Messages.Should().Equal(TK.Common.Errors.UNKNOWN);
    }

    [Fact]
    public void MonadLaw_Associativity_OnSuccessChain()
    {
        // Associativity: m.Bind(f).Bind(g) ≡ m.Bind(x => f(x).Bind(g))
        var m = D2Result<int>.Ok(3);
        Func<int, D2Result<int>> f = x => D2Result<int>.Ok(x + 1);
        Func<int, D2Result<int>> g = x => D2Result<int>.Ok(x * 2);

        var lhs = m.Bind(f).Bind(g);
        var rhs = m.Bind(x => f(x).Bind(g));

        lhs.Success.Should().Be(rhs.Success);
        lhs.Data.Should().Be(rhs.Data);
    }

    [Fact]
    public void MonadLaw_Associativity_OnFailureChain()
    {
        // Failure short-circuits both grouping orders identically.
        var m = D2Result<int>.NotFound();
        Func<int, D2Result<int>> f = x => D2Result<int>.Ok(x + 1);
        Func<int, D2Result<int>> g = x => D2Result<int>.Ok(x * 2);

        var lhs = m.Bind(f).Bind(g);
        var rhs = m.Bind(x => f(x).Bind(g));

        lhs.Success.Should().BeFalse();
        rhs.Success.Should().BeFalse();
        lhs.ErrorCode.Should().Be(rhs.ErrorCode);
    }
}
