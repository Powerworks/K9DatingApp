using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ApproveVolunteer;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ApproveVolunteerHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class ApproveVolunteerHandlerTests
{
    private static VolunteerApplication BuildUnderReview()
    {
        var application = VolunteerApplication.ApplyNew(Guid.NewGuid(), VolunteerAreaOfInterest.HomeChecks).VolunteerApplication;
        application.Review();
        return application;
    }

    [Fact]
    public async Task Handle_WhenUnderReview_ApprovesAndPersists()
    {
        var application = BuildUnderReview();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);

        var result = await ApproveVolunteerHandler.Handle(application.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ApproveVolunteerResponse>>();
        application.Status.Should().Be(VolunteerApplicationStatus.Approved);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.VolunteerApprovedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var applicationId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<VolunteerApplication>(applicationId, null, out _);

        var result = await ApproveVolunteerHandler.Handle(applicationId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotUnderReview_ReturnsConflict()
    {
        var application = VolunteerApplication.ApplyNew(Guid.NewGuid(), VolunteerAreaOfInterest.HomeChecks).VolunteerApplication;
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out _);

        var result = await ApproveVolunteerHandler.Handle(application.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
