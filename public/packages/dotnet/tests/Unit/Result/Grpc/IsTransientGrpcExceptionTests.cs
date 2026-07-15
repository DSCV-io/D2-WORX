// -----------------------------------------------------------------------
// <copyright file="IsTransientGrpcExceptionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result.Grpc;

using AwesomeAssertions;
using DcsvIo.D2.Result.Grpc;
using global::Grpc.Core;
using Xunit;

/// <summary>
/// Pins the transient/non-transient classification in
/// <see cref="ProtoExtensions.IsTransientGrpcException"/>.
/// </summary>
public sealed class IsTransientGrpcExceptionTests
{
    [Theory]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.ResourceExhausted)]
    [InlineData(StatusCode.Aborted)]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.Unavailable)]
    public void TransientCodes_ReturnTrue(StatusCode code)
        => ProtoExtensions.IsTransientGrpcException(Ex(code)).Should().BeTrue();

    [Theory]
    [InlineData(StatusCode.OK)]
    [InlineData(StatusCode.Cancelled)]
    [InlineData(StatusCode.InvalidArgument)]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.AlreadyExists)]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.Unauthenticated)]
    [InlineData(StatusCode.FailedPrecondition)]
    [InlineData(StatusCode.OutOfRange)]
    [InlineData(StatusCode.Unimplemented)]
    [InlineData(StatusCode.DataLoss)]
    public void NonTransientCodes_ReturnFalse(StatusCode code)
        => ProtoExtensions.IsTransientGrpcException(Ex(code)).Should().BeFalse();

    [Fact]
    public void NullException_Throws_ArgumentNullException()
    {
        var act = () => ProtoExtensions.IsTransientGrpcException(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static RpcException Ex(StatusCode code) =>
        new(new Status(code, string.Empty));
}
