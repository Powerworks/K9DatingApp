using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.UpdateListingStatus;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - UpdateListingStatusHandler calls
/// FetchForWriting/AppendOne/SaveChangesAsync against DogListing plus a
/// plain LoadAsync against ShelterAccount for the ownership check
/// (read-only, not a self-load - ADR-031).
/// </summary>
public class UpdateListingStatusHandlerTests
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
    public async Task Handle_WhenCallerOwnsTheListing_UpdatesStatusAndPersists()
    {
        var (shelterAccount, dogListing) = SeedListing();
        var session = BuildSession(shelterAccount, dogListing, out var stream);

        var result = await UpdateListingStatusHandler.Handle(
            dogListing.Id, new UpdateListingStatusRequest(DogListingStatus.Available), BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<UpdateListingStatusResponse>>();
        ((Ok<UpdateListingStatusResponse>)result.Result).Value!.Status.Should().Be(DogListingStatus.Available);

        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingStatusUpdatedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenListingDoesNotExist_ReturnsNotFound()
    {
        var dogListingId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<DogListing>(dogListingId, null, out _);

        var result = await UpdateListingStatusHandler.Handle(
            dogListingId, new UpdateListingStatusRequest(DogListingStatus.Available), BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnTheListing_ReturnsForbid()
    {
        var (shelterAccount, dogListing) = SeedListing();
        var session = BuildSession(shelterAccount, dogListing, out _);

        var result = await UpdateListingStatusHandler.Handle(
            dogListing.Id, new UpdateListingStatusRequest(DogListingStatus.Available), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenTargetStatusIsAdopted_ReturnsConflictAndDoesNotPersist()
    {
        var (shelterAccount, dogListing) = SeedListing();
        var session = BuildSession(shelterAccount, dogListing, out var stream);

        var result = await UpdateListingStatusHandler.Handle(
            dogListing.Id, new UpdateListingStatusRequest(DogListingStatus.Adopted), BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        dogListing.Status.Should().Be(DogListingStatus.NotReadyYet, "the guard must run before UpdateStatus is called");
        stream.DidNotReceiveWithAnyArgs().AppendOne(default!);
    }

    [Fact]
    public async Task Handle_WhenCurrentStatusIsAdopted_ReturnsConflictRegardlessOfTargetStatus()
    {
        var (shelterAccount, dogListing) = SeedListing();
        dogListing.UpdateStatus(DogListingStatus.Adopted);
        var session = BuildSession(shelterAccount, dogListing, out var stream);

        var result = await UpdateListingStatusHandler.Handle(
            dogListing.Id, new UpdateListingStatusRequest(DogListingStatus.Available), BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        stream.DidNotReceiveWithAnyArgs().AppendOne(default!);
    }

    [Fact]
    public async Task Handle_WhenAFosterPlacementIsActive_ReturnsConflictAndDoesNotPersist()
    {
        var (shelterAccount, dogListing) = SeedListing();
        dogListing.PlaceInFoster(Guid.NewGuid());
        var session = BuildSession(shelterAccount, dogListing, out var stream);

        var result = await UpdateListingStatusHandler.Handle(
            dogListing.Id, new UpdateListingStatusRequest(DogListingStatus.NotReadyYet), BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        stream.DidNotReceiveWithAnyArgs().AppendOne(default!);
    }
}
