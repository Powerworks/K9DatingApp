using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Identity.Api.Commands.SubmitFeedback;
using K9Crush.Modules.Identity.Contracts;
using K9Crush.Modules.Identity.Domain;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - SubmitFeedbackHandler only calls
/// Store/SaveChangesAsync (no LoadAsync even - Feedback is always newly
/// created), so IDocumentSession mocks cleanly here.
/// </summary>
public class SubmitFeedbackHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenCalled_StoresFeedbackAndReturnsItsId()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();

        var (result, integrationEvent) = await SubmitFeedbackHandler.Handle(
            new SubmitFeedbackRequest("The onboarding flow was confusing."), BuildUser(ownerId), session, CancellationToken.None);

        result.Should().BeOfType<Ok<SubmitFeedbackResponse>>();
        result.Value!.FeedbackId.Should().NotBeEmpty();

        integrationEvent.Should().NotBeNull();
        integrationEvent.OwnerId.Should().Be(ownerId);
        integrationEvent.Message.Should().Be("The onboarding flow was confusing.");
        integrationEvent.FeedbackId.Should().Be(result.Value.FeedbackId);

        session.Received(1).Store(Arg.Is<Feedback[]>(arr =>
arr != null &&             arr.Length == 1 && arr[0].OwnerId == ownerId && arr[0].Message == "The onboarding flow was confusing."));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
