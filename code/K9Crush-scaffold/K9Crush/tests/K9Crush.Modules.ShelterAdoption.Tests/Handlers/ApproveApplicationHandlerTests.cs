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
/// Layer 2 (TestingApproach.md) - ApproveApplicationHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class ApproveApplicationHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnTheShelter_ReturnsForbidAndNoIntegrationEvent()
    {
        var shelterAccount = ShelterAccount.Create(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var application = Application.Submit(ApplicantOwnerId, DogListingId, shelterAccount.Id);
        application.Review();

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var (result, integrationEvent) = await ApproveApplicationHandler.Handle(
            application.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUnderReview_ApprovesAndCascadesApplicationApprovedWithDogName()
    {
        var shelterAccount = ShelterAccount.Create(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var application = Application.Submit(ApplicantOwnerId, DogListingId, shelterAccount.Id);
        application.Review();
        var dogListing = DogListing.Create(shelterAccount.Id, "Biscuit", "Beagle mix", 24, "Friendly");

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);
        session.LoadAsync<DogListing>(DogListingId, Arg.Any<CancellationToken>()).Returns(dogListing);

        var (result, integrationEvent) = await ApproveApplicationHandler.Handle(
            application.Id, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ApproveApplicationResponse>>();
        application.Status.Should().Be(ApplicationStatus.Approved);

        integrationEvent.Should().NotBeNull();
        integrationEvent!.ApplicationId.Should().Be(application.Id);
        integrationEvent.ApplicantOwnerId.Should().Be(ApplicantOwnerId);
        integrationEvent.DogName.Should().Be("Biscuit");
    }
}
