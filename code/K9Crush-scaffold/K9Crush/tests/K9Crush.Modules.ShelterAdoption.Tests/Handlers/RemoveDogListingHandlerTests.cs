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
/// Layer 2 (TestingApproach.md) - RemoveDogListingHandler calls
/// FetchForWriting/AppendOne/SaveChangesAsync against DogListing (no more
/// session.Delete under ADR-031's no-hard-delete pattern - Remove() flags
/// IsRemoved instead) plus a plain LoadAsync against ShelterAccount for
/// the ownership check.
/// </summary>
public class RemoveDogListingHandlerTests
{
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFoundAndNoIntegrationEvent()
    {
        var dogListingId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<DogListing>(dogListingId, null, out _);

        var (result, integrationEvent) = await RemoveDogListingHandler.Handle(dogListingId, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenCallerOwnsTheListing_FlagsRemovedAndCascadesDogListingRemovedWithDogName()
    {
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var dogListing = DogListing.AddNew(shelterAccount.Id, "Biscuit", "Beagle mix", 24, "Friendly").DogListing;

        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(dogListing.Id, dogListing, out var stream);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var (result, integrationEvent) = await RemoveDogListingHandler.Handle(dogListing.Id, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        dogListing.IsRemoved.Should().BeTrue();
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingWithdrawnV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        integrationEvent.Should().NotBeNull();
        integrationEvent!.DogListingId.Should().Be(dogListing.Id);
        integrationEvent.ShelterAccountId.Should().Be(shelterAccount.Id);
        integrationEvent.DogName.Should().Be("Biscuit");
    }
}
