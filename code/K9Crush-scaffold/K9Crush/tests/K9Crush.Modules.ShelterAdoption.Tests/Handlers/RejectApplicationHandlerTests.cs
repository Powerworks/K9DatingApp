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
/// Layer 2 (TestingApproach.md) - RejectApplicationHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
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
        var session = Substitute.For<IDocumentSession>();
        var applicationId = Guid.NewGuid();
        session.LoadAsync<Application>(applicationId, Arg.Any<CancellationToken>()).Returns((Application?)null);

        var (result, integrationEvent) = await RejectApplicationHandler.Handle(
            applicationId, new RejectApplicationRequest("Not a fit"), BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUnderReview_RejectsAndCascadesApplicationRejectedWithDogName()
    {
        var shelterAccount = ShelterAccount.Create(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var application = Application.Submit(ApplicantOwnerId, DogListingId, shelterAccount.Id);
        application.Review();
        var dogListing = DogListing.Create(shelterAccount.Id, "Biscuit", "Beagle mix", 24, "Friendly");

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);
        session.LoadAsync<DogListing>(DogListingId, Arg.Any<CancellationToken>()).Returns(dogListing);

        var (result, integrationEvent) = await RejectApplicationHandler.Handle(
            application.Id, new RejectApplicationRequest("Not enough yard space"), BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RejectApplicationResponse>>();
        application.Status.Should().Be(ApplicationStatus.Rejected);

        integrationEvent.Should().NotBeNull();
        integrationEvent!.ApplicationId.Should().Be(application.Id);
        integrationEvent.ApplicantOwnerId.Should().Be(ApplicantOwnerId);
        integrationEvent.DogName.Should().Be("Biscuit");
        integrationEvent.RejectionReason.Should().Be("Not enough yard space");
    }
}
