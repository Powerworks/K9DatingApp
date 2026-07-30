using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ApproveFosterCaregiver;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ApproveFosterCaregiverHandler only
/// calls FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession
/// mocks cleanly here (ADR-031).
/// </summary>
public class ApproveFosterCaregiverHandlerTests
{
    private static FosterApplication BuildUnderReview()
    {
        var application = FosterApplication.ApplyNew(Guid.NewGuid(), HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1)).FosterApplication;
        application.Review();
        return application;
    }

    [Fact]
    public async Task Handle_WhenUnderReview_ApprovesAndPersists()
    {
        var application = BuildUnderReview();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);

        var result = await ApproveFosterCaregiverHandler.Handle(application.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ApproveFosterCaregiverResponse>>();
        application.Status.Should().Be(FosterApplicationStatus.Approved);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.FosterCaregiverApprovedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var applicationId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<FosterApplication>(applicationId, null, out _);

        var result = await ApproveFosterCaregiverHandler.Handle(applicationId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotUnderReview_ReturnsConflict()
    {
        var application = FosterApplication.ApplyNew(Guid.NewGuid(), HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1)).FosterApplication;
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out _);

        var result = await ApproveFosterCaregiverHandler.Handle(application.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
