using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.RejectApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RejectApplicationHandler calls
/// FetchForWriting/AppendOne/SaveChangesAsync against Application plus
/// plain LoadAsync calls against ShelterAccount (ownership check) and
/// DogListing (read-only, for the cascaded event's DogName) - ADR-031.
/// </summary>
public class RejectApplicationHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFoundAndNoIntegrationEvent()
    {
        var applicationId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<Application>(applicationId, null, out _);

        var (result, integrationEvent) = await RejectApplicationHandler.Handle(
            applicationId, new RejectApplicationRequest("Not a fit"), BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUnderReview_RejectsAndCascadesApplicationRejectedWithDogName()
    {
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, shelterAccount.Id, TestIntake.Default).Application;
        application.Review();
        var dogListing = DogListing.AddNew(shelterAccount.Id, "Biscuit", "Beagle mix", 24, "Friendly").DogListing;

        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application.Id, application, out var stream);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);
        session.LoadAsync<DogListing>(DogListingId, Arg.Any<CancellationToken>()).Returns(dogListing);

        var (result, integrationEvent) = await RejectApplicationHandler.Handle(
            application.Id, new RejectApplicationRequest("Not enough yard space"), BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RejectApplicationResponse>>();
        application.Status.Should().Be(ApplicationStatus.Rejected);
        stream.Received(1).AppendOne(Arg.Any<object>());

        integrationEvent.Should().NotBeNull();
        integrationEvent!.ApplicationId.Should().Be(application.Id);
        integrationEvent.ApplicantOwnerId.Should().Be(ApplicantOwnerId);
        integrationEvent.DogName.Should().Be("Biscuit");
        integrationEvent.RejectionReason.Should().Be("Not enough yard space");
    }
}
