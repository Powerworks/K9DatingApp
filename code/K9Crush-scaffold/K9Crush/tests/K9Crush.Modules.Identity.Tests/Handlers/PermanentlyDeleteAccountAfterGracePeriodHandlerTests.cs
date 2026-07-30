using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Identity.Api.Automations.PermanentlyDeleteAccountAfterGracePeriod;
using K9Crush.Modules.Identity.Domain;
using K9Crush.Modules.Identity.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - PermanentlyDeleteAccountAfterGracePeriodHandler
/// only calls FetchForWriting/AppendOne/SaveChangesAsync, so
/// IDocumentSession mocks cleanly here (ADR-031).
/// </summary>
public class PermanentlyDeleteAccountAfterGracePeriodHandlerTests
{
    [Fact]
    public async Task Handle_WhenOwnerDoesNotExist_DoesNothing()
    {
        var ownerId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<OwnerAccount>(ownerId, null, out _);

        await PermanentlyDeleteAccountAfterGracePeriodHandler.Handle(
            new CheckAccountGracePeriodExpired(ownerId), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenAlreadyPermanentlyDeleted_DoesNothing()
    {
        var (owner, _) = OwnerAccount.CreateNew(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(-1);
        owner.PermanentlyDelete();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(owner.Id, owner, out _);

        await PermanentlyDeleteAccountAfterGracePeriodHandler.Handle(
            new CheckAccountGracePeriodExpired(owner.Id), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenOwnerRecoveredDuringGracePeriod_DoesNothing()
    {
        var (owner, _) = OwnerAccount.CreateNew(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(30);
        owner.RecoverAccount(); // GracePeriodEndsAt cleared
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(owner.Id, owner, out _);

        await PermanentlyDeleteAccountAfterGracePeriodHandler.Handle(
            new CheckAccountGracePeriodExpired(owner.Id), session, CancellationToken.None);

        owner.IsPermanentlyDeleted.Should().BeFalse();
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenGracePeriodHasNotElapsedYet_DoesNothing()
    {
        var (owner, _) = OwnerAccount.CreateNew(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(30); // still 30 days out
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(owner.Id, owner, out _);

        await PermanentlyDeleteAccountAfterGracePeriodHandler.Handle(
            new CheckAccountGracePeriodExpired(owner.Id), session, CancellationToken.None);

        owner.IsPermanentlyDeleted.Should().BeFalse();
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenGracePeriodHasElapsed_PermanentlyDeletesAndPersists()
    {
        var (owner, _) = OwnerAccount.CreateNew(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(-1); // already in the past
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(owner.Id, owner, out var stream);

        await PermanentlyDeleteAccountAfterGracePeriodHandler.Handle(
            new CheckAccountGracePeriodExpired(owner.Id), session, CancellationToken.None);

        owner.IsPermanentlyDeleted.Should().BeTrue();
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(OwnerAccountPermanentlyDeletedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
