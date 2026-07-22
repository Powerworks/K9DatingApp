using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.RemoveDogListing;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RemoveDogListingHandler only calls
/// LoadAsync/Delete/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here.
/// </summary>
public class RemoveDogListingHandlerTests
{
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFoundAndNoIntegrationEvent()
    {
        var session = Substitute.For<IDocumentSession>();
        var dogListingId = Guid.NewGuid();
        session.LoadAsync<DogListing>(dogListingId, Arg.Any<CancellationToken>()).Returns((DogListing?)null);

        var (result, integrationEvent) = await RemoveDogListingHandler.Handle(dogListingId, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenCallerOwnsTheListing_DeletesAndCascadesDogListingRemovedWithDogName()
    {
        var shelterAccount = ShelterAccount.Create(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var dogListing = DogListing.Create(shelterAccount.Id, "Biscuit", "Beagle mix", 24, "Friendly");

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<DogListing>(dogListing.Id, Arg.Any<CancellationToken>()).Returns(dogListing);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var (result, integrationEvent) = await RemoveDogListingHandler.Handle(dogListing.Id, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        session.Received(1).Delete(dogListing);

        integrationEvent.Should().NotBeNull();
        integrationEvent!.DogListingId.Should().Be(dogListing.Id);
        integrationEvent.ShelterAccountId.Should().Be(shelterAccount.Id);
        integrationEvent.DogName.Should().Be("Biscuit");
    }
}
