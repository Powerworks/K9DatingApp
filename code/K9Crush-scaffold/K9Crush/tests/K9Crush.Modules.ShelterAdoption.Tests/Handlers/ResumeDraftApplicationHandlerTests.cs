using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ResumeDraftApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ResumeDraftApplicationHandler only calls
/// LoadAsync/Store/SaveChangesAsync (no session.Query&lt;T&gt;() LINQ), so
/// IDocumentSession mocks cleanly here. Covers the handler's two
/// consolidated outcomes (resume normally vs. the dog listing having been
/// removed) - this is the automated stand-in for what would otherwise have
/// needed a manual curl scenario deleting a real DogListing row.
/// </summary>
public class ResumeDraftApplicationHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var session = Substitute.For<IDocumentSession>();
        var applicationId = Guid.NewGuid();
        session.LoadAsync<Application>(applicationId, Arg.Any<CancellationToken>()).Returns((Application?)null);

        var result = await ResumeDraftApplicationHandler.Handle(
            applicationId, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheApplicant_ReturnsForbid()
    {
        var application = Application.StartDraft(ApplicantOwnerId, DogListingId, ShelterAccountId);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await ResumeDraftApplicationHandler.Handle(
            application.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenApplicationIsNotADraft_ReturnsConflict()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default); // Status = Pending
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await ResumeDraftApplicationHandler.Handle(
            application.Id, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenDraftAndDogListingStillExists_ReturnsDraftWithoutPersisting()
    {
        var application = Application.StartDraft(ApplicantOwnerId, DogListingId, ShelterAccountId);
        var dogListing = DogListing.Create(ShelterAccountId, "Rex", "Labrador", 24, "Loves fetch");
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        session.LoadAsync<DogListing>(application.DogListingId, Arg.Any<CancellationToken>()).Returns(dogListing);

        var result = await ResumeDraftApplicationHandler.Handle(
            application.Id, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ResumeDraftApplicationResponse>>();
        var ok = (Ok<ResumeDraftApplicationResponse>)result.Result;
        ok.Value!.Status.Should().Be(nameof(ApplicationStatus.Draft));
        application.Status.Should().Be(ApplicationStatus.Draft, "resuming a still-available draft shouldn't change its status");
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenDraftAndDogListingWasRemoved_ClosesDraftAndPersists()
    {
        var application = Application.StartDraft(ApplicantOwnerId, DogListingId, ShelterAccountId);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        session.LoadAsync<DogListing>(application.DogListingId, Arg.Any<CancellationToken>()).Returns((DogListing?)null);

        var result = await ResumeDraftApplicationHandler.Handle(
            application.Id, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ResumeDraftApplicationResponse>>();
        var ok = (Ok<ResumeDraftApplicationResponse>)result.Result;
        ok.Value!.Status.Should().Be(nameof(ApplicationStatus.ClosedDogNoLongerAvailable));
        application.Status.Should().Be(ApplicationStatus.ClosedDogNoLongerAvailable);
        session.Received(1).Store(Arg.Is<Application[]>(arr => arr != null && arr.Length == 1 && arr[0] == application));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
