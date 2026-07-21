using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.EditDogListing;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - EditDogListingHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class EditDogListingHandlerTests
{
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static (ShelterAccount shelterAccount, DogListing dogListing) SeedListing()
    {
        var shelterAccount = ShelterAccount.Create(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var dogListing = DogListing.Create(shelterAccount.Id, "Biscuit", "Beagle mix", 24, "Friendly");
        return (shelterAccount, dogListing);
    }

    [Fact]
    public async Task Handle_WhenSignificantChangeIsTrue_CascadesDogListingSignificantlyEdited()
    {
        var (shelterAccount, dogListing) = SeedListing();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var (result, integrationEvent) = await EditDogListingHandler.Handle(
            dogListing.Id,
            new EditDogListingRequest("Biscuit", "Beagle mix", 30, "Now a senior dog", SignificantChange: true),
            BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<EditDogListingResponse>>();
        integrationEvent.Should().NotBeNull();
        integrationEvent!.DogListingId.Should().Be(dogListing.Id);
        integrationEvent.DogName.Should().Be("Biscuit");
    }

    [Fact]
    public async Task Handle_WhenSignificantChangeIsFalse_EditsButCascadesNothing()
    {
        var (shelterAccount, dogListing) = SeedListing();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var (result, integrationEvent) = await EditDogListingHandler.Handle(
            dogListing.Id,
            new EditDogListingRequest("Biscuit", "Beagle mix", 24, "Typo fix", SignificantChange: false),
            BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<EditDogListingResponse>>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFoundAndNoIntegrationEvent()
    {
        var session = Substitute.For<IDocumentSession>();
        var dogListingId = Guid.NewGuid();
        session.LoadAsync<DogListing>(dogListingId, Arg.Any<CancellationToken>()).Returns((DogListing?)null);

        var (result, integrationEvent) = await EditDogListingHandler.Handle(
            dogListingId,
            new EditDogListingRequest("Biscuit", "Beagle mix", 24, "Friendly", SignificantChange: true),
            BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }
}
