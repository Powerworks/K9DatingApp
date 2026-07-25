using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ReviewFosterApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ReviewFosterApplicationHandler only
/// calls FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession
/// mocks cleanly here (ADR-031).
/// </summary>
public class ReviewFosterApplicationHandlerTests
{
    private static FosterApplication BuildSubmitted() =>
        FosterApplication.ApplyNew(Guid.NewGuid(), HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1)).FosterApplication;

    [Fact]
    public async Task Handle_WhenSubmitted_ReviewsAndPersists()
    {
        var application = BuildSubmitted();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);

        var result = await ReviewFosterApplicationHandler.Handle(application.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ReviewFosterApplicationResponse>>();
        application.Status.Should().Be(FosterApplicationStatus.UnderReview);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.FosterApplicationReviewedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var applicationId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<FosterApplication>(applicationId, null, out _);

        var result = await ReviewFosterApplicationHandler.Handle(applicationId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotSubmitted_ReturnsConflict()
    {
        var application = BuildSubmitted();
        application.Review();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out _);

        var result = await ReviewFosterApplicationHandler.Handle(application.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
