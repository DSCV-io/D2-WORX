// -----------------------------------------------------------------------
// <copyright file="UpdateTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Unit.Infra.Messaging;

using System.Net;
using D2.Events.Protos.V1;
using D2.Geo.App.Interfaces.Messaging.Handlers.Pub;
using D2.Geo.Infra.Messaging.Handlers.Pub;
using D2.Geo.Infra.Messaging.Publishers;
using D2.Shared.Handler;
using D2.Shared.Messaging.RabbitMQ;
using D2.Shared.Result;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using Xunit;

/// <summary>
/// Unit tests for the <see cref="Update"/> handler.
/// </summary>
public class UpdateTests
{
    private readonly Mock<UpdatePublisher> r_publisherMock;
    private readonly IHandlerContext r_context;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTests"/> class.
    /// </summary>
    public UpdateTests()
    {
        r_publisherMock = new Mock<UpdatePublisher>(
            new Mock<ProtoPublisher>(
                Mock.Of<IConnection>(),
                Mock.Of<ILogger<ProtoPublisher>>()).Object,
            Mock.Of<ILogger<UpdatePublisher>>());
        r_context = CreateHandlerContext();
    }

    /// <summary>
    /// Tests that HandleAsync returns success when publisher succeeds.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task HandleAsync_WhenPublisherSucceeds_ReturnsSuccess()
    {
        // Arrange
        r_publisherMock
            .Setup(x => x.PublishAsync(It.IsAny<GeoRefDataUpdatedEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(D2Result.Ok());

        var handler = new Update(r_publisherMock.Object, r_context);
        var input = new IPubs.UpdateInput("1.0.0");

        // Act
        var result = await handler.HandleAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        r_publisherMock.Verify(
            x => x.PublishAsync(
                It.Is<GeoRefDataUpdatedEvent>(m => m.Version == "1.0.0"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that HandleAsync returns failure when publisher fails.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task HandleAsync_WhenPublisherFails_ReturnsFailure()
    {
        // Arrange
        r_publisherMock
            .Setup(x => x.PublishAsync(It.IsAny<GeoRefDataUpdatedEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(D2Result.Fail(["Failed"], HttpStatusCode.ServiceUnavailable));

        var handler = new Update(r_publisherMock.Object, r_context);
        var input = new IPubs.UpdateInput("1.0.0");

        // Act
        var result = await handler.HandleAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>
    /// Tests that HandleAsync passes the correct version to the publisher.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task HandleAsync_PassesCorrectVersionToPublisher()
    {
        // Arrange
        GeoRefDataUpdatedEvent? capturedMessage = null;
        r_publisherMock
            .Setup(x => x.PublishAsync(It.IsAny<GeoRefDataUpdatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<GeoRefDataUpdatedEvent, CancellationToken>((msg, _) => capturedMessage = msg)
            .ReturnsAsync(D2Result.Ok());

        var handler = new Update(r_publisherMock.Object, r_context);
        var input = new IPubs.UpdateInput("2.5.3");

        // Act
        await handler.HandleAsync(input, CancellationToken.None);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Version.Should().Be("2.5.3");
    }

    private static IHandlerContext CreateHandlerContext()
    {
        var requestContext = new Mock<IRequestContext>();
        requestContext.Setup(x => x.TraceId).Returns("test-trace-id");

        var logger = new Mock<ILogger>();

        var context = new Mock<IHandlerContext>();
        context.Setup(x => x.Request).Returns(requestContext.Object);
        context.Setup(x => x.Logger).Returns(logger.Object);

        return context.Object;
    }
}
