using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.RejectFosterApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RejectFosterApplicationHandler only
/// calls FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession
/// mocks cleanly here (ADR-031).
/// </summary>
public class RejectFosterApplicationHandlerTests
{
    private static FosterApplication BuildUnderReview()
    {
        var application = FosterApplication.ApplyNew(Guid.NewGuid(), HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1)).FosterApplication;
        application.Review();
        return application;
    }

    [Fact]
    public async Task Handle_WhenUnderReview_RejectsAndPersists()
    {
        var application = BuildUnderReview();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);

        var result = await RejectFosterApplicationHandler.Handle(
            application.Id, new RejectFosterApplicationRequest("Home visit could not confirm a secure garden"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RejectFosterApplicationResponse>>();
        application.Status.Should().Be(FosterApplicationStatus.Rejected);
        application.RejectionReason.Should().Be("Home visit could not confirm a secure garden");
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.FosterApplicationRejectedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var applicationId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<FosterApplication>(applicationId, null, out _);

        var result = await RejectFosterApplicationHandler.Handle(
            applicationId, new RejectFosterApplicationRequest("reason"), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotUnderReview_ReturnsConflict()
    {
        var application = FosterApplication.ApplyNew(Guid.NewGuid(), HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1)).FosterApplication;
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out _);

        var result = await RejectFosterApplicationHandler.Handle(
            application.Id, new RejectFosterApplicationRequest("reason"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
