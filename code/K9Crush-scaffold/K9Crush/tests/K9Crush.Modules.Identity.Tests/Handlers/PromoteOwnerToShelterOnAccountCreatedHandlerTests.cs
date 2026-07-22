using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Identity.Api.Automations.PromoteOwnerToShelterOnAccountCreated;
using K9Crush.Modules.Identity.Domain;
using K9Crush.Modules.ShelterAdoption.Contracts;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - PromoteOwnerToShelterOnAccountCreatedHandler
/// only calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here.
/// </summary>
public class PromoteOwnerToShelterOnAccountCreatedHandlerTests
{
    private static ShelterAccountCreatedV1 BuildEvent(Guid ownerId) => new(
        EventId: Guid.NewGuid(), OccurredAt: DateTimeOffset.UtcNow, ShelterAccountId: Guid.NewGuid(), OwnerId: ownerId);

    [Fact]
    public async Task Handle_WhenOwnerAccountDoesNotExist_DoesNothing()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(ownerId, Arg.Any<CancellationToken>()).Returns((OwnerAccount?)null);

        await PromoteOwnerToShelterOnAccountCreatedHandler.Handle(BuildEvent(ownerId), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenOwnerIsAlreadyShelter_DoesNothing()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "shelter@example.com", DateTimeOffset.UtcNow);
        owner.PromoteToShelter();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        await PromoteOwnerToShelterOnAccountCreatedHandler.Handle(BuildEvent(owner.Id), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenOwnerIsPlainOwner_PromotesToShelterAndPersists()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        await PromoteOwnerToShelterOnAccountCreatedHandler.Handle(BuildEvent(owner.Id), session, CancellationToken.None);

        owner.Role.Should().Be(OwnerRole.Shelter);
        session.Received(1).Store(Arg.Is<OwnerAccount[]>(arr => arr != null && arr.Length == 1 && arr[0] == owner));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
