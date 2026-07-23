using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ApproveVolunteer;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ApproveVolunteerHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class ApproveVolunteerHandlerTests
{
    private static VolunteerApplication BuildUnderReview()
    {
        var application = VolunteerApplication.Apply(Guid.NewGuid(), VolunteerAreaOfInterest.HomeChecks);
        application.Review();
        return application;
    }

    [Fact]
    public async Task Handle_WhenUnderReview_ApprovesAndPersists()
    {
        var application = BuildUnderReview();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<VolunteerApplication>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await ApproveVolunteerHandler.Handle(application.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ApproveVolunteerResponse>>();
        session.Received(1).Store(Arg.Is<VolunteerApplication[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].Status == VolunteerApplicationStatus.Approved));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var applicationId = Guid.NewGuid();
        session.LoadAsync<VolunteerApplication>(applicationId, Arg.Any<CancellationToken>()).Returns((VolunteerApplication?)null);

        var result = await ApproveVolunteerHandler.Handle(applicationId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotUnderReview_ReturnsConflict()
    {
        var application = VolunteerApplication.Apply(Guid.NewGuid(), VolunteerAreaOfInterest.HomeChecks);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<VolunteerApplication>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await ApproveVolunteerHandler.Handle(application.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
