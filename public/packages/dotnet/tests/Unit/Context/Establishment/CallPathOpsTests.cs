// -----------------------------------------------------------------------
// <copyright file="CallPathOpsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Context.Establishment;

using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using Xunit;

/// <summary>
/// Unit matrix for the pure, depth-bounded call-path append helper. Covers the
/// fresh-start path, the append path, the oldest-trim-beyond-the-bound invariant
/// (a serialized path exceeding the bound is dropped wholesale by the
/// receiver), and the required-self-id guard.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CallPathOpsTests
{
    private static readonly DateTimeOffset sr_t0 =
        new(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Append_NullExisting_StartsSingleEntryPath()
    {
        var result = CallPathOps.Append(null, "edge", CallPathKind.Edge, sr_t0);

        result.Should().ContainSingle();
        result[0].Id.Should().Be("edge");
        result[0].Kind.Should().Be(CallPathKind.Edge);
        result[0].Timestamp.Should().Be(sr_t0);
    }

    [Fact]
    public void Append_EmptyExisting_StartsSingleEntryPath()
    {
        var result = CallPathOps.Append([], "edge", CallPathKind.Edge, sr_t0);

        result.Should().ContainSingle();
        result[0].Id.Should().Be("edge");
    }

    [Fact]
    public void Append_NonEmptyExisting_AppendsAsNewestEntry()
    {
        var existing = new List<CallPathEntry> { new("edge", CallPathKind.Edge, sr_t0) };

        var result = CallPathOps.Append(
            existing, "key-custodian", CallPathKind.WorkloadHop, sr_t0.AddSeconds(1));

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("edge");
        result[1].Id.Should().Be("key-custodian");
        result[1].Kind.Should().Be(CallPathKind.WorkloadHop);
    }

    [Fact]
    public void Append_DoesNotMutateTheInputList()
    {
        var existing = new List<CallPathEntry> { new("edge", CallPathKind.Edge, sr_t0) };

        CallPathOps.Append(existing, "files", CallPathKind.WorkloadHop, sr_t0);

        existing.Should().ContainSingle("the input list is never mutated — a new list is returned");
    }

    [Fact]
    public void Append_AtTheDepthBound_TrimsOldestToStayBounded()
    {
        // Build a path exactly at the bound, then append one more — the result must be
        // capped at MAX_CALL_PATH_DEPTH with the OLDEST entry trimmed and the newest kept.
        var atBound = Enumerable
            .Range(0, CallPathOps.MAX_CALL_PATH_DEPTH)
            .Select(i => new CallPathEntry($"hop-{i}", CallPathKind.WorkloadHop, sr_t0))
            .ToList();

        var result = CallPathOps.Append(atBound, "newest", CallPathKind.WorkloadHop, sr_t0);

        result.Should().HaveCount(CallPathOps.MAX_CALL_PATH_DEPTH);
        result[^1].Id.Should().Be("newest", "the just-appended hop is always retained");
        result.Should().NotContain(e => e.Id == "hop-0", "the oldest entry is trimmed");
        result[0].Id.Should().Be("hop-1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Append_NullOrBlankId_Throws(string? id)
    {
        var act = () => CallPathOps.Append(null, id!, CallPathKind.Edge, sr_t0);

        act.Should().Throw<ArgumentException>(
            "a missing self-identity is a misconfiguration, not a silently-dropped entry");
    }
}
