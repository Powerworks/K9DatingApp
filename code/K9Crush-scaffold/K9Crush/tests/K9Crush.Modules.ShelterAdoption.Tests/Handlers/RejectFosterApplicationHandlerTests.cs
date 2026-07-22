using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.RejectFosterApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RejectFosterApplicationHandler only
/// calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here.
/// </summary>
public class RejectFosterApplicationHandlerTests
{
    private static FosterApplication BuildUnderReview()
    {
        var application = FosterApplication.Apply(Guid.NewGuid(), HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1));
        application.Review();
        return application;
    }

    [Fact]
    public async Task Handle_WhenUnderReview_RejectsAndPersists()
    {
        var application = BuildUnderReview();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FosterApplication>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await RejectFosterApplicationHandler.Handle(
            application.Id, new RejectFosterApplicationRequest("Home visit could not confirm a secure garden"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RejectFosterApplicationResponse>>();
        session.Received(1).Store(Arg.Is<FosterApplication[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].Status == FosterApplicationStatus.Rejected &&
            arr[0].RejectionReason == "Home visit could not confirm a secure garden"));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var applicationId = Guid.NewGuid();
        session.LoadAsync<FosterApplication>(applicationId, Arg.Any<CancellationToken>()).Returns((FosterApplication?)null);

        var result = await RejectFosterApplicationHandler.Handle(
            applicationId, new RejectFosterApplicationRequest("reason"), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotUnderReview_ReturnsConflict()
    {
        var application = FosterApplication.Apply(Guid.NewGuid(), HomeType.House, hasGarden: true, hasOtherPets: false, new DateOnly(2026, 8, 1));
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FosterApplication>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await RejectFosterApplicationHandler.Handle(
            application.Id, new RejectFosterApplicationRequest("reason"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }
}
