using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ReviewSurrenderRequest;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ReviewSurrenderRequestHandler only
/// calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here.
/// </summary>
public class ReviewSurrenderRequestHandlerTests
{
    private static readonly Guid RequestedByOwnerId = Guid.NewGuid();

    private static DogSurrenderRequest BuildRequested() => DogSurrenderRequest.Request(
        RequestedByOwnerId, "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy");

    [Fact]
    public async Task Handle_WhenRequested_ReviewsAndPersists()
    {
        var surrenderRequest = BuildRequested();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);

        var result = await ReviewSurrenderRequestHandler.Handle(surrenderRequest.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ReviewSurrenderRequestResponse>>();
        ((Ok<ReviewSurrenderRequestResponse>)result.Result).Value!.Status.Should().Be(nameof(SurrenderRequestStatus.UnderReview));
        session.Received(1).Store(Arg.Is<DogSurrenderRequest[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].Status == SurrenderRequestStatus.UnderReview));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var surrenderRequestId = Guid.NewGuid();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequestId, Arg.Any<CancellationToken>()).Returns((DogSurrenderRequest?)null);

        var result = await ReviewSurrenderRequestHandler.Handle(surrenderRequestId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotInRequestedStatus_ReturnsConflict()
    {
        var surrenderRequest = BuildRequested();
        surrenderRequest.Review();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogSurrenderRequest>(surrenderRequest.Id, Arg.Any<CancellationToken>()).Returns(surrenderRequest);

        var result = await ReviewSurrenderRequestHandler.Handle(surrenderRequest.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
