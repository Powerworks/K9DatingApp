using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ReviewSurrenderRequest;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ReviewSurrenderRequestHandler only
/// calls FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession
/// mocks cleanly here (ADR-031).
/// </summary>
public class ReviewSurrenderRequestHandlerTests
{
    private static readonly Guid RequestedByOwnerId = Guid.NewGuid();

    private static DogSurrenderRequest BuildRequested() => DogSurrenderRequest.RequestNew(
        RequestedByOwnerId, "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle", "Healthy").DogSurrenderRequest;

    [Fact]
    public async Task Handle_WhenRequested_ReviewsAndPersists()
    {
        var surrenderRequest = BuildRequested();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out var stream);

        var result = await ReviewSurrenderRequestHandler.Handle(surrenderRequest.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ReviewSurrenderRequestResponse>>();
        ((Ok<ReviewSurrenderRequestResponse>)result.Result).Value!.Status.Should().Be(nameof(SurrenderRequestStatus.UnderReview));
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.SurrenderRequestReviewedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        var surrenderRequestId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<DogSurrenderRequest>(surrenderRequestId, null, out _);

        var result = await ReviewSurrenderRequestHandler.Handle(surrenderRequestId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotInRequestedStatus_ReturnsConflict()
    {
        var surrenderRequest = BuildRequested();
        surrenderRequest.Review();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(surrenderRequest.Id, surrenderRequest, out _);

        var result = await ReviewSurrenderRequestHandler.Handle(surrenderRequest.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
