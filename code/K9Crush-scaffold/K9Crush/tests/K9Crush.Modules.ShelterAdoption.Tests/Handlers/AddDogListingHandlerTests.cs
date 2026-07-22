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
/// Layer 2 (TestingApproach.md) - AddDogListingHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
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
        var shelterAccount = ShelterAccount.Create(OwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
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
        var shelterAccount = ShelterAccount.Create(OwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()); // Requested, not Created
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await AddDogListingHandler.Handle(shelterAccount.Id, BuildRequest(), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenActivatedAndCallerOwnsIt_AddsListingAndPersists()
    {
        var shelterAccount = ShelterAccount.Create(OwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        shelterAccount.Verify();
        shelterAccount.Activate();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await AddDogListingHandler.Handle(shelterAccount.Id, BuildRequest(), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<AddDogListingResponse>>();
        ((Ok<AddDogListingResponse>)result.Result).Value!.DogListingId.Should().NotBeEmpty();

        session.Received(1).Store(Arg.Is<DogListing[]>(arr =>
arr != null &&             arr.Length == 1 && arr[0].ShelterAccountId == shelterAccount.Id && arr[0].Name == "Biscuit"));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
