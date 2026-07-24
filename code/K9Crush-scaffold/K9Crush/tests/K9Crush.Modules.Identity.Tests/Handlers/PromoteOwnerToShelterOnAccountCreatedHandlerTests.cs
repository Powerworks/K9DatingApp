using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Identity.Api.Automations.PromoteOwnerToShelterOnAccountCreated;
using K9Crush.Modules.Identity.Domain;
using K9Crush.Modules.Identity.Domain.Events;
using K9Crush.Modules.ShelterAdoption.Contracts;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - PromoteOwnerToShelterOnAccountCreatedHandler
/// only calls FetchForWriting/AppendOne/SaveChangesAsync, so
/// IDocumentSession mocks cleanly here (ADR-031).
/// </summary>
public class PromoteOwnerToShelterOnAccountCreatedHandlerTests
{
    private static ShelterAccountCreatedV1 BuildEvent(Guid ownerId) => new(
        EventId: Guid.NewGuid(), OccurredAt: DateTimeOffset.UtcNow, ShelterAccountId: Guid.NewGuid(), OwnerId: ownerId);

    [Fact]
    public async Task Handle_WhenOwnerAccountDoesNotExist_DoesNothing()
    {
        var ownerId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<OwnerAccount>(ownerId, null, out _);

        await PromoteOwnerToShelterOnAccountCreatedHandler.Handle(BuildEvent(ownerId), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenOwnerIsAlreadyShelter_DoesNothing()
    {
        var (owner, _) = OwnerAccount.CreateNew(Guid.NewGuid(), "shelter@example.com", DateTimeOffset.UtcNow);
        owner.PromoteToShelter();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(owner.Id, owner, out _);

        await PromoteOwnerToShelterOnAccountCreatedHandler.Handle(BuildEvent(owner.Id), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenOwnerIsPlainOwner_PromotesToShelterAndPersists()
    {
        var (owner, _) = OwnerAccount.CreateNew(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(owner.Id, owner, out var stream);

        await PromoteOwnerToShelterOnAccountCreatedHandler.Handle(BuildEvent(owner.Id), session, CancellationToken.None);

        owner.Role.Should().Be(OwnerRole.Shelter);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(OwnerAccountPromotedToShelterV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
