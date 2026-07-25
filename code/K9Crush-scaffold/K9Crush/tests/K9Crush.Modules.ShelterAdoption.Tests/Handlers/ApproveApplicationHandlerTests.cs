using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ApproveApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ApproveApplicationHandler is a genuine
/// two-stream write: FetchForWriting/AppendOne against both Application
/// and (when it still exists) DogListing, plus a plain LoadAsync against
/// ShelterAccount for the ownership check - ADR-031.
/// </summary>
public class ApproveApplicationHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static IDocumentSession BuildSession(
        ShelterAccount shelterAccount, Application? application, DogListing? dogListing,
        out JasperFx.Events.IEventStream<Application> applicationStream,
        out JasperFx.Events.IEventStream<DogListing> dogListingStream)
    {
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application?.Id ?? Guid.NewGuid(), application, out applicationStream);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        dogListingStream = Substitute.For<JasperFx.Events.IEventStream<DogListing>>();
        dogListingStream.Aggregate.Returns(dogListing);
        session.Events.FetchForWriting<DogListing>(DogListingId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(dogListingStream));

        return session;
    }

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnTheShelter_ReturnsForbidAndNoIntegrationEvent()
    {
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, shelterAccount.Id, TestIntake.Default).Application;
        application.Review();

        var session = BuildSession(shelterAccount, application, null, out _, out _);

        var (result, integrationEvent) = await ApproveApplicationHandler.Handle(
            application.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUnderReview_ApprovesAndCascadesApplicationApprovedWithDogName()
    {
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, shelterAccount.Id, TestIntake.Default).Application;
        application.Review();
        var dogListing = DogListing.AddNew(shelterAccount.Id, "Biscuit", "Beagle mix", 24, "Friendly").DogListing;

        var session = BuildSession(shelterAccount, application, dogListing, out var applicationStream, out var dogListingStream);

        var (result, integrationEvent) = await ApproveApplicationHandler.Handle(
            application.Id, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ApproveApplicationResponse>>();
        application.Status.Should().Be(ApplicationStatus.Approved);
        dogListing.Status.Should().Be(DogListingStatus.Adopted, "v3 ENRICHMENT: approval cascades the listing's status");

        applicationStream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.ApplicationApprovalV1)));
        dogListingStream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingStatusUpdatedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        integrationEvent.Should().NotBeNull();
        integrationEvent!.ApplicationId.Should().Be(application.Id);
        integrationEvent.ApplicantOwnerId.Should().Be(ApplicantOwnerId);
        integrationEvent.DogName.Should().Be("Biscuit");
    }

    [Fact]
    public async Task Handle_WhenTheDogListingNoLongerExists_StillApprovesAndCascadesWithBlankDogName()
    {
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, shelterAccount.Id, TestIntake.Default).Application;
        application.Review();

        var session = BuildSession(shelterAccount, application, null, out _, out var dogListingStream);

        var (result, integrationEvent) = await ApproveApplicationHandler.Handle(
            application.Id, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ApproveApplicationResponse>>();
        integrationEvent!.DogName.Should().BeEmpty();
        dogListingStream.DidNotReceiveWithAnyArgs().AppendOne(default!);
    }
}
