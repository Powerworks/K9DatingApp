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
/// Layer 2 (TestingApproach.md) - ResumeDraftApplicationHandler calls
/// FetchForWriting/AppendOne/SaveChangesAsync against Application plus a
/// plain LoadAsync against DogListing (read-only availability check, not
/// a self-load - ADR-031). Covers the handler's two consolidated outcomes
/// (resume normally vs. the dog listing having been removed).
/// </summary>
public class ResumeDraftApplicationHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static IDocumentSession BuildSession(Application? application, DogListing? dogListing, out JasperFx.Events.IEventStream<Application> stream)
    {
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application?.Id ?? Guid.NewGuid(), application, out stream);
        session.LoadAsync<DogListing>(DogListingId, Arg.Any<CancellationToken>()).Returns(dogListing);
        return session;
    }

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var applicationId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<Application>(applicationId, null, out _);

        var result = await ResumeDraftApplicationHandler.Handle(
            applicationId, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheApplicant_ReturnsForbid()
    {
        var application = Application.StartDraftNew(ApplicantOwnerId, DogListingId, ShelterAccountId).Application;
        var session = BuildSession(application, null, out _);

        var result = await ResumeDraftApplicationHandler.Handle(
            application.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenApplicationIsNotADraft_ReturnsConflict()
    {
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default).Application; // Status = Pending
        var session = BuildSession(application, null, out _);

        var result = await ResumeDraftApplicationHandler.Handle(
            application.Id, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenDraftAndDogListingStillExists_ReturnsDraftWithoutPersisting()
    {
        var application = Application.StartDraftNew(ApplicantOwnerId, DogListingId, ShelterAccountId).Application;
        var dogListing = DogListing.AddNew(ShelterAccountId, "Rex", "Labrador", 24, "Loves fetch").DogListing;
        var session = BuildSession(application, dogListing, out var stream);

        var result = await ResumeDraftApplicationHandler.Handle(
            application.Id, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ResumeDraftApplicationResponse>>();
        var ok = (Ok<ResumeDraftApplicationResponse>)result.Result;
        ok.Value!.Status.Should().Be(nameof(ApplicationStatus.Draft));
        application.Status.Should().Be(ApplicationStatus.Draft, "resuming a still-available draft shouldn't change its status");
        stream.DidNotReceiveWithAnyArgs().AppendOne(default!);
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenDraftAndDogListingWasRemoved_ClosesDraftAndPersists()
    {
        var application = Application.StartDraftNew(ApplicantOwnerId, DogListingId, ShelterAccountId).Application;
        var session = BuildSession(application, null, out var stream);

        var result = await ResumeDraftApplicationHandler.Handle(
            application.Id, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ResumeDraftApplicationResponse>>();
        var ok = (Ok<ResumeDraftApplicationResponse>)result.Result;
        ok.Value!.Status.Should().Be(nameof(ApplicationStatus.ClosedDogNoLongerAvailable));
        application.Status.Should().Be(ApplicationStatus.ClosedDogNoLongerAvailable);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.ApplicationClosedDogNoLongerAvailableV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDraftAndDogListingWasWithdrawn_ClosesDraftAndPersists()
    {
        var application = Application.StartDraftNew(ApplicantOwnerId, DogListingId, ShelterAccountId).Application;
        var dogListing = DogListing.AddNew(ShelterAccountId, "Rex", "Labrador", 24, "Loves fetch").DogListing;
        dogListing.Remove();
        var session = BuildSession(application, dogListing, out var stream);

        var result = await ResumeDraftApplicationHandler.Handle(
            application.Id, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ResumeDraftApplicationResponse>>();
        application.Status.Should().Be(ApplicationStatus.ClosedDogNoLongerAvailable, "IsRemoved must be treated the same as the listing no longer existing");
        stream.Received(1).AppendOne(Arg.Any<object>());
    }
}
