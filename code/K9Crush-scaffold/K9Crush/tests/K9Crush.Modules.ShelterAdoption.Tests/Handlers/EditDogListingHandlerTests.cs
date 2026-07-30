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
/// Layer 2 (TestingApproach.md) - EditDogListingHandler calls
/// FetchForWriting/AppendOne/SaveChangesAsync against DogListing plus a
/// plain LoadAsync against ShelterAccount for the ownership check
/// (read-only, not a self-load - ADR-031).
/// </summary>
public class EditDogListingHandlerTests
{
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static (ShelterAccount shelterAccount, DogListing dogListing) SeedListing()
    {
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var dogListing = DogListing.AddNew(shelterAccount.Id, "Biscuit", "Beagle mix", 24, "Friendly").DogListing;
        return (shelterAccount, dogListing);
    }

    private static IDocumentSession BuildSession(ShelterAccount shelterAccount, DogListing? dogListing, out JasperFx.Events.IEventStream<DogListing> stream)
    {
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(dogListing?.Id ?? Guid.NewGuid(), dogListing, out stream);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);
        return session;
    }

    [Fact]
    public async Task Handle_WhenSignificantChangeIsTrue_CascadesDogListingSignificantlyEdited()
    {
        var (shelterAccount, dogListing) = SeedListing();
        var session = BuildSession(shelterAccount, dogListing, out _);

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
        var session = BuildSession(shelterAccount, dogListing, out var stream);

        var (result, integrationEvent) = await EditDogListingHandler.Handle(
            dogListing.Id,
            new EditDogListingRequest("Biscuit", "Beagle mix", 24, "Typo fix", SignificantChange: false),
            BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<EditDogListingResponse>>();
        integrationEvent.Should().BeNull();
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingEditedV1)));
    }

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFoundAndNoIntegrationEvent()
    {
        var dogListingId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<DogListing>(dogListingId, null, out _);

        var (result, integrationEvent) = await EditDogListingHandler.Handle(
            dogListingId,
            new EditDogListingRequest("Biscuit", "Beagle mix", 24, "Friendly", SignificantChange: true),
            BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }
}
