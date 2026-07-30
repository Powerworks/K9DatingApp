using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetShelterDogListings;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.ShelterAdoption;

/// <summary>
/// Layer 3 (TestingApproach.md) - GetShelterDogListingsHandler calls
/// session.Query&lt;DogListing&gt;().Where(...).ToListAsync(), the LINQ path
/// Layer 2's IQuerySession mocks can't reach. Scoped to a per-test random
/// shelterAccountId, so safe to share ShelterAdoptionPostgresFixture via
/// [Collection(...)] - not the global "every row" class of check that
/// forced GetAdoptionListingsIntegrationTests onto its own dedicated
/// container instead. Seeding now goes through Events.StartStream
/// (ADR-031) rather than session.Store, since both ShelterAccount and
/// DogListing are event-sourced.
/// </summary>
[Collection(ShelterAdoptionPostgresCollection.Name)]
public class GetShelterDogListingsIntegrationTests(ShelterAdoptionPostgresFixture fixture)
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFound()
    {
        var shelterAccountId = Guid.NewGuid();
        await using var session = fixture.Store.LightweightSession();

        var result = await GetShelterDogListingsHandler.Handle(shelterAccountId, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnTheShelterAccount_ReturnsForbid()
    {
        var ownerId = Guid.NewGuid();
        var (shelterAccount, shelterAccountRequested) = ShelterAccount.RequestNew(ownerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Events.StartStream<ShelterAccount>(shelterAccount.Id, shelterAccountRequested);
            await seedSession.SaveChangesAsync();
        }

        await using var session = fixture.Store.LightweightSession();
        var result = await GetShelterDogListingsHandler.Handle(shelterAccount.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyListingsForThatShelter()
    {
        var ownerId = Guid.NewGuid();
        var (shelterAccount, shelterAccountRequested) = ShelterAccount.RequestNew(ownerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var (ownListing, ownListingAdded) = DogListing.AddNew(shelterAccount.Id, "Biscuit", "Labrador", 36, "Friendly");
        var (otherShelterListing, otherListingAdded) = DogListing.AddNew(Guid.NewGuid(), "Max", "Beagle", 24, "Playful");

        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Events.StartStream<ShelterAccount>(shelterAccount.Id, shelterAccountRequested);
            seedSession.Events.StartStream<DogListing>(ownListing.Id, ownListingAdded);
            seedSession.Events.StartStream<DogListing>(otherShelterListing.Id, otherListingAdded);
            await seedSession.SaveChangesAsync();
        }

        await using var session = fixture.Store.LightweightSession();
        var result = await GetShelterDogListingsHandler.Handle(shelterAccount.Id, BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ShelterDogListingsResponse>>();
        var response = ((Ok<ShelterDogListingsResponse>)result.Result).Value!;
        var item = response.Items.Should().ContainSingle().Which;
        item.DogListingId.Should().Be(ownListing.Id);
        item.Status.Should().Be(DogListingStatus.NotReadyYet);
    }
}
