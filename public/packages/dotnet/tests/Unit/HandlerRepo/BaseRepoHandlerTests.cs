// -----------------------------------------------------------------------
// <copyright file="BaseRepoHandlerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.HandlerRepo;

using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Handler.Repo;
using DcsvIo.D2.Handler.Repo.Abstractions;
using DcsvIo.D2.Result;
using DcsvIo.D2.Tests.Unit.Handler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Adversarial coverage of <see cref="BaseRepoHandler{TSelf,TInput,TOutput}"/>.
/// This is the security-critical seam between EF / provider exceptions and
/// typed <see cref="D2Result"/> outcomes — bugs here cause silent retries on
/// programmer errors (Unhandled → looks like a transient failure to a generic
/// retry policy) or pass-through of unhandled exceptions that should have been
/// classified.
/// </summary>
public sealed class BaseRepoHandlerTests
{
    private const string _TRACE_ID = "trace-bh-1";

    // ----------------------------------------------------------------------
    // Happy path
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ExecuteReturnsOk_ReturnsOk_ClassifierNeverCalled()
    {
        var classifier = new StubClassifier(returnValue: null);
        var handler = TestRepoHandler.Returning(D2Result<string?>.Ok());
        var built = handler.WithClassifier(classifier);

        var result = await built.HandleAsync("input");

        result.Success.Should().BeTrue();
        classifier.LastSeen.Should().BeNull(
            "classifier MUST NOT be called when ExecuteAsync succeeds");
    }

    // ----------------------------------------------------------------------
    // BCL-typed concurrency: DbUpdateConcurrencyException → direct dispatch
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_DbUpdateConcurrency_ReturnsConflict_ClassifierNotCalled()
    {
        var classifier = new StubClassifier(returnValue: DbFailureKind.UniqueViolation);
        var ex = new DbUpdateConcurrencyException("concurrency");
        var handler = TestRepoHandler.Throwing(ex).WithClassifier(classifier);

        var result = await handler.HandleAsync("input");

        result.Failed.Should().BeTrue();
        result.IsConcurrencyConflict.Should().BeTrue();
        result.TraceId.Should().Be(_TRACE_ID);
        classifier.LastSeen.Should().BeNull(
            "BCL-typed DbUpdateConcurrencyException is dispatched directly — "
            + "classifier is NOT consulted");
    }

    [Fact]
    public async Task HandleAsync_DbUpdateConcurrency_MapDbExceptionReturnsCustom_OverrideWins()
    {
        var custom = D2Result<string?>.ConcurrencyConflict(traceId: "custom-trace");
        var ex = new DbUpdateConcurrencyException("concurrency");
        var handler = new MapOverrideHandler(
            ex,
            mapResult: custom,
            classifier: new StubClassifier(returnValue: null));

        var result = await handler.HandleAsync("input");

        result.Should().BeSameAs(custom);
    }

    [Fact]
    public async Task HandleAsync_DbUpdateConcurrency_MapDbExceptionReturnsNull_DefaultUsed()
    {
        var ex = new DbUpdateConcurrencyException("concurrency");
        var handler = new MapOverrideHandler(
            ex,
            mapResult: null,
            classifier: new StubClassifier(returnValue: null));

        var result = await handler.HandleAsync("input");

        result.IsConcurrencyConflict.Should().BeTrue();
        result.TraceId.Should().Be(_TRACE_ID);
    }

    [Fact]
    public async Task HandleAsync_DbUpdateConcurrency_MapCalledWithConcurrencyConflictKind()
    {
        var ex = new DbUpdateConcurrencyException("concurrency");
        var handler = new MapOverrideHandler(
            ex,
            mapResult: null,
            classifier: new StubClassifier(returnValue: null));

        await handler.HandleAsync("input");

        handler.LastMapKind.Should().Be(DbFailureKind.ConcurrencyConflict);
        handler.LastMapException.Should().BeSameAs(ex);
    }

    // ----------------------------------------------------------------------
    // Generic exception → classifier returns null → unhandled preserved
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_GenericExceptionClassifiedNull_PreservesUnhandledException()
    {
        var classifier = new StubClassifier(returnValue: null);
        var ex = new InvalidOperationException("boom");
        var handler = TestRepoHandler.Throwing(ex).WithClassifier(classifier);

        var result = await handler.HandleAsync("input");

        result.Failed.Should().BeTrue();
        result.IsUnhandledException.Should().BeTrue();
        result.TraceId.Should().Be(_TRACE_ID);
        classifier.LastSeen.Should().BeSameAs(ex);
    }

    // ----------------------------------------------------------------------
    // Classifier dispatch — every DbFailureKind value
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ClassifierUniqueViolation_DispatchesToUniqueViolationFactory()
    {
        var result = await DispatchAsync(DbFailureKind.UniqueViolation);

        result.IsUniqueViolation.Should().BeTrue();
        result.TraceId.Should().Be(_TRACE_ID);
    }

    [Fact]
    public async Task HandleAsync_ClassifierForeignKeyViolation_DispatchesToForeignKeyFactory()
    {
        var result = await DispatchAsync(DbFailureKind.ForeignKeyViolation);

        result.IsForeignKeyViolation.Should().BeTrue();
        result.TraceId.Should().Be(_TRACE_ID);
    }

    [Fact]
    public async Task HandleAsync_ClassifierNotNullViolation_DispatchesToNotNullFactory()
    {
        var result = await DispatchAsync(DbFailureKind.NotNullViolation);

        result.IsNotNullViolation.Should().BeTrue();
        result.TraceId.Should().Be(_TRACE_ID);
    }

    [Fact]
    public async Task HandleAsync_ClassifierCheckViolation_DispatchesToCheckViolationFactory()
    {
        var result = await DispatchAsync(DbFailureKind.CheckViolation);

        result.IsCheckViolation.Should().BeTrue();
        result.TraceId.Should().Be(_TRACE_ID);
    }

    [Fact]
    public async Task HandleAsync_ClassifierTimeout_DispatchesToDbTimeoutFactory()
    {
        var result = await DispatchAsync(DbFailureKind.Timeout);

        result.IsDbTimeout.Should().BeTrue();
        result.TraceId.Should().Be(_TRACE_ID);
    }

    [Fact]
    public async Task HandleAsync_ClassifierDeadlock_DispatchesToDbDeadlockFactory()
    {
        var result = await DispatchAsync(DbFailureKind.Deadlock);

        result.IsDbDeadlock.Should().BeTrue();
        result.TraceId.Should().Be(_TRACE_ID);
    }

    [Fact]
    public async Task HandleAsync_ClassifierConnectionFailure_DispatchesToConnectionFactory()
    {
        var result = await DispatchAsync(DbFailureKind.ConnectionFailure);

        result.IsDbConnectionFailure.Should().BeTrue();
        result.TraceId.Should().Be(_TRACE_ID);
    }

    [Fact]
    public async Task HandleAsync_ClassifierReturnsConcurrencyConflictForGenericEx_Dispatches()
    {
        // Adversarial: even though the BCL-typed path catches concurrency
        // conflicts directly, a CUSTOM classifier might return
        // ConcurrencyConflict for a non-DbUpdateConcurrencyException — the
        // dispatch table must still cover that arm correctly.
        var result = await DispatchAsync(DbFailureKind.ConcurrencyConflict);

        result.IsConcurrencyConflict.Should().BeTrue();
        result.TraceId.Should().Be(_TRACE_ID);
    }

    // ----------------------------------------------------------------------
    // MapDbException for non-concurrency cases
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_NonConcurrencyMapReturnsCustom_OverrideWins_ClassifierWasCalled()
    {
        var custom = D2Result<string?>.UniqueViolation(traceId: "custom-uv");
        var classifier = new StubClassifier(returnValue: DbFailureKind.UniqueViolation);
        var ex = new InvalidOperationException("dup");
        var handler = new MapOverrideHandler(ex, mapResult: custom, classifier: classifier);

        var result = await handler.HandleAsync("input");

        result.Should().BeSameAs(custom);
        classifier.LastSeen.Should().BeSameAs(
            ex,
            "classifier IS consulted on non-BCL-concurrency paths");
        handler.LastMapException.Should().BeSameAs(
            ex,
            "MapDbException must receive the original exception, not a wrapper");
        handler.LastMapKind.Should().Be(DbFailureKind.UniqueViolation);
    }

    [Fact]
    public async Task HandleAsync_NonConcurrencyMapReturnsNull_DefaultFactoryUsed()
    {
        var classifier = new StubClassifier(returnValue: DbFailureKind.UniqueViolation);
        var handler = new MapOverrideHandler(
            new InvalidOperationException("dup"),
            mapResult: null,
            classifier: classifier);

        var result = await handler.HandleAsync("input");

        result.IsUniqueViolation.Should().BeTrue();
        result.TraceId.Should().Be(_TRACE_ID);
    }

    // ----------------------------------------------------------------------
    // Adversarial — invalid enum value crashes loudly
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ClassifierReturnsInvalidEnum_ThrowsArgumentOutOfRange()
    {
        // Adversarial: the wildcard arm in DispatchDefault throws rather
        // than returning a degraded result. A future DbFailureKind value
        // added without a corresponding case here MUST crash loudly the
        // first time the new kind is dispatched, not silently degrade
        // into UnhandledException. This test pins that safety net.
        var classifier = new StubClassifier(returnValue: (DbFailureKind)999);
        var handler = TestRepoHandler.Throwing(new InvalidOperationException("x"))
            .WithClassifier(classifier);

        Func<Task> act = async () => await handler.HandleAsync("input");

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    // ----------------------------------------------------------------------
    // Constructor — null classifier behavior
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Ctor_NullClassifier_ThrowsOnFirstUseOfNonConcurrencyPath()
    {
        // Adversarial: BaseRepoHandler stores the classifier without a
        // null-guard. A null classifier from misconfigured DI doesn't
        // crash at construction — it crashes on first use that needs
        // classification. Documents actual behavior.
        var ctx = MakeContext();
        var ex = new InvalidOperationException("boom");
        var handler = new TestRepoHandler(
            ctx, classifier: null!, action: TestRepoHandler.Throw(ex));

        Func<Task> act = async () => await handler.HandleAsync("input");

        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task Ctor_NullClassifier_ConcurrencyPath_DoesNotConsultClassifier()
    {
        // Adversarial corollary: DbUpdateConcurrencyException path bypasses
        // the classifier, so a null classifier doesn't crash on this path.
        // Documents the partial-failure surface area.
        var ctx = MakeContext();
        var ex = new DbUpdateConcurrencyException("boom");
        var handler = new TestRepoHandler(
            ctx, classifier: null!, action: TestRepoHandler.Throw(ex));

        var result = await handler.HandleAsync("input");

        result.IsConcurrencyConflict.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // TraceId threading
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_TraceId_ThreadsThroughOnDefaultDispatch()
    {
        const string custom_trace = "custom-trace-zzz";
        var ctx = MakeContext(custom_trace);
        var classifier = new StubClassifier(returnValue: DbFailureKind.Deadlock);
        var handler = new TestRepoHandler(
            ctx,
            classifier,
            action: TestRepoHandler.Throw(new InvalidOperationException("dl")));

        var result = await handler.HandleAsync("input");

        result.TraceId.Should().Be(custom_trace);
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static async Task<D2Result<string?>> DispatchAsync(DbFailureKind kind)
    {
        var classifier = new StubClassifier(returnValue: kind);
        var handler = TestRepoHandler.Throwing(new InvalidOperationException("x"))
            .WithClassifier(classifier);

        return await handler.HandleAsync("input");
    }

    private static HandlerContext<TestRepoHandler> MakeContext(string traceId = _TRACE_ID)
    {
        var request = new TestRequestContext { TraceId = traceId };
        return new HandlerContext<TestRepoHandler>(
            request,
            NullLogger<TestRepoHandler>.Instance);
    }

    /// <summary>
    /// Hand-rolled <see cref="IDbExceptionClassifier"/> for tests. Records
    /// the last exception passed in for verification, returns the configured
    /// <see cref="DbFailureKind"/>.
    /// </summary>
    private sealed class StubClassifier(DbFailureKind? returnValue) : IDbExceptionClassifier
    {
        public Exception? LastSeen { get; private set; }

        public DbFailureKind? Classify(Exception exception)
        {
            LastSeen = exception;
            return returnValue;
        }
    }

    /// <summary>
    /// Test BaseRepoHandler subclass — runs an injected delegate as
    /// ExecuteAsync, plumbed for both "return result" and "throw exception"
    /// scenarios.
    /// </summary>
    private sealed class TestRepoHandler : BaseRepoHandler<TestRepoHandler, string, string>
    {
        private readonly Func<string, ValueTask<D2Result<string?>>> r_action;

        public TestRepoHandler(
            HandlerContext<TestRepoHandler> context,
            IDbExceptionClassifier classifier,
            Func<string, ValueTask<D2Result<string?>>> action)
            : base(context, classifier)
        {
            r_action = action;
        }

        public static TestRepoHandlerBuilder Returning(D2Result<string?> result) =>
            new(_ => new ValueTask<D2Result<string?>>(result));

        public static TestRepoHandlerBuilder Throwing(Exception ex) =>
            new(Throw(ex));

        public static Func<string, ValueTask<D2Result<string?>>> Throw(Exception ex) =>
            _ => throw ex;

        protected override ValueTask<D2Result<string?>> ExecuteAsync(
            string input,
            CancellationToken ct) => r_action(input);
    }

    /// <summary>
    /// Fluent builder for TestRepoHandler so call sites don't have to
    /// instantiate the context inline every time.
    /// </summary>
    private sealed class TestRepoHandlerBuilder
    {
        private readonly Func<string, ValueTask<D2Result<string?>>> r_action;

        public TestRepoHandlerBuilder(Func<string, ValueTask<D2Result<string?>>> action)
        {
            r_action = action;
        }

        public TestRepoHandler WithClassifier(IDbExceptionClassifier classifier)
        {
            var ctx = MakeContext();
            return new TestRepoHandler(ctx, classifier, r_action);
        }
    }

    /// <summary>
    /// TestRepoHandler variant that overrides MapDbException to return a
    /// configured result, AND records what kind / exception it was called
    /// with — for verifying the override hook is wired correctly.
    /// </summary>
    private sealed class MapOverrideHandler : BaseRepoHandler<MapOverrideHandler, string, string>
    {
        private readonly Exception r_throwOnExecute;
        private readonly D2Result<string?>? r_mapResult;

        public MapOverrideHandler(
            Exception throwOnExecute,
            D2Result<string?>? mapResult,
            IDbExceptionClassifier classifier)
            : base(MakeMapContext(), classifier)
        {
            r_throwOnExecute = throwOnExecute;
            r_mapResult = mapResult;
        }

        public Exception? LastMapException { get; private set; }

        public DbFailureKind? LastMapKind { get; private set; }

        protected override D2Result<string?>? MapDbException(
            Exception exception, DbFailureKind kind)
        {
            LastMapException = exception;
            LastMapKind = kind;
            return r_mapResult;
        }

        protected override ValueTask<D2Result<string?>> ExecuteAsync(
            string input,
            CancellationToken ct) => throw r_throwOnExecute;

        private static HandlerContext<MapOverrideHandler> MakeMapContext()
        {
            var request = new TestRequestContext { TraceId = _TRACE_ID };
            return new HandlerContext<MapOverrideHandler>(
                request,
                NullLogger<MapOverrideHandler>.Instance);
        }
    }
}
