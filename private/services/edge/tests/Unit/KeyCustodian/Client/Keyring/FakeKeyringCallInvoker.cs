// -----------------------------------------------------------------------
// <copyright file="FakeKeyringCallInvoker.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Client.Keyring;

using System.Threading.Tasks;
using global::D2.Services.Protos.KeyCustodian.V2Alpha;
using global::Grpc.Core;

/// <summary>
/// A <see cref="CallInvoker"/> double that returns a canned unary response (or a faulted
/// response task) for the keyring gRPC client — no server / socket required.
/// </summary>
internal sealed class FakeKeyringCallInvoker(Func<GetKeyringResponse> responder) : CallInvoker
{
    public static FakeKeyringCallInvoker Returns(GetKeyringResponse response)
        => new(() => response);

    public static FakeKeyringCallInvoker Faults(RpcException fault)
        => new(() => throw fault);

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        Task<TResponse> responseTask;

        try
        {
            responseTask = Task.FromResult((TResponse)(object)responder());
        }
        catch (RpcException ex)
        {
            responseTask = Task.FromException<TResponse>(ex);
        }

        return new AsyncUnaryCall<TResponse>(
            responseTask,
            Task.FromResult(new Metadata()),
            static () => Status.DefaultSuccess,
            static () => new Metadata(),
            static () => { });
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => throw new NotSupportedException();

    public override AsyncClientStreamingCall<TRequest, TResponse>
        AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options)
        => throw new NotSupportedException();

    public override AsyncServerStreamingCall<TResponse>
        AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => throw new NotSupportedException();

    public override AsyncDuplexStreamingCall<TRequest, TResponse>
        AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options)
        => throw new NotSupportedException();
}
