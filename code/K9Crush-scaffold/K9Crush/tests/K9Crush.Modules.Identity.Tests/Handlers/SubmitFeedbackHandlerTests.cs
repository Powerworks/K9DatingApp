using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Identity.Api.Commands.SubmitFeedback;
using K9Crush.Modules.Identity.Contracts;
using K9Crush.Modules.Identity.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - SubmitFeedbackHandler only calls
/// Events.StartStream/SaveChangesAsync (no fetch even - Feedback is
/// always newly created), so IDocumentSession mocks cleanly here
/// (ADR-031).
/// </summary>
public class SubmitFeedbackHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenCalled_StartsStreamAndReturnsItsId()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        var eventStore = Substitute.For<Marten.Events.IEventStoreOperations>();
        session.Events.Returns(eventStore);

        var (result, integrationEvent) = await SubmitFeedbackHandler.Handle(
            new SubmitFeedbackRequest("The onboarding flow was confusing."), BuildUser(ownerId), session, CancellationToken.None);

        result.Should().BeOfType<Ok<SubmitFeedbackResponse>>();
        result.Value!.FeedbackId.Should().NotBeEmpty();

        integrationEvent.Should().NotBeNull();
        integrationEvent.OwnerId.Should().Be(ownerId);
        integrationEvent.Message.Should().Be("The onboarding flow was confusing.");
        integrationEvent.FeedbackId.Should().Be(result.Value.FeedbackId);

        eventStore.Received(1).StartStream<K9Crush.Modules.Identity.Domain.Feedback>(
            result.Value.FeedbackId,
            Arg.Is<object[]>(events => events != null && events.Length == 1 && events[0] != null
                && ((FeedbackRecordedV1)events[0]).OwnerId == ownerId
                && ((FeedbackRecordedV1)events[0]).Message == "The onboarding flow was confusing."));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
