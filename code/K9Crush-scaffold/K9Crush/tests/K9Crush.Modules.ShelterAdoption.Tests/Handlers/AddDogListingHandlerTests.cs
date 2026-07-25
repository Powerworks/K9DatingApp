using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.AddDogListing;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - AddDogListingHandler calls
/// Events.StartStream/SaveChangesAsync against a new DogListing plus a
/// plain LoadAsync against ShelterAccount for the ownership check
/// (ADR-031).
/// </summary>
public class AddDogListingHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static AddDogListingRequest BuildRequest() => new("Biscuit", "Labrador", 36, "Friendly");

    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFound()
    {
        var shelterAccountId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccountId, Arg.Any<CancellationToken>()).Returns((ShelterAccount?)null);

        var result = await AddDogListingHandler.Handle(shelterAccountId, BuildRequest(), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnTheShelterAccount_ReturnsForbid()
    {
        var shelterAccount = ShelterAccount.RequestNew(OwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        shelterAccount.Verify();
        shelterAccount.Activate();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await AddDogListingHandler.Handle(shelterAccount.Id, BuildRequest(), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenShelterAccountIsNotActivated_ReturnsConflict()
    {
        var shelterAccount = ShelterAccount.RequestNew(OwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount; // Requested, not Created
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await AddDogListingHandler.Handle(shelterAccount.Id, BuildRequest(), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenActivatedAndCallerOwnsIt_AddsListingAndPersists()
    {
        var shelterAccount = ShelterAccount.RequestNew(OwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        shelterAccount.Verify();
        shelterAccount.Activate();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await AddDogListingHandler.Handle(shelterAccount.Id, BuildRequest(), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<AddDogListingResponse>>();
        var dogListingId = ((Ok<AddDogListingResponse>)result.Result).Value!.DogListingId;
        dogListingId.Should().NotBeEmpty();

        session.Events.Received(1).StartStream<DogListing>(
            dogListingId,
            Arg.Is<object[]>(events => events != null && events.Length == 1 && events[0] != null
                && ((K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingAddedV1)events[0]).ShelterAccountId == shelterAccount.Id
                && ((K9Crush.Modules.ShelterAdoption.Domain.Events.DogListingAddedV1)events[0]).Name == "Biscuit"));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
