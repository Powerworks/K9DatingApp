using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Identity.Api.Automations.PermanentlyDeleteAccountAfterGracePeriod;
using K9Crush.Modules.Identity.Domain;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - PermanentlyDeleteAccountAfterGracePeriodHandler
/// only calls LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here.
/// </summary>
public class PermanentlyDeleteAccountAfterGracePeriodHandlerTests
{
    [Fact]
    public async Task Handle_WhenOwnerDoesNotExist_DoesNothing()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(ownerId, Arg.Any<CancellationToken>()).Returns((OwnerAccount?)null);

        await PermanentlyDeleteAccountAfterGracePeriodHandler.Handle(
            new CheckAccountGracePeriodExpired(ownerId), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenAlreadyPermanentlyDeleted_DoesNothing()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(-1);
        owner.PermanentlyDelete();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        await PermanentlyDeleteAccountAfterGracePeriodHandler.Handle(
            new CheckAccountGracePeriodExpired(owner.Id), session, CancellationToken.None);

        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenOwnerRecoveredDuringGracePeriod_DoesNothing()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(30);
        owner.RecoverAccount(); // GracePeriodEndsAt cleared
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        await PermanentlyDeleteAccountAfterGracePeriodHandler.Handle(
            new CheckAccountGracePeriodExpired(owner.Id), session, CancellationToken.None);

        owner.IsPermanentlyDeleted.Should().BeFalse();
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenGracePeriodHasNotElapsedYet_DoesNothing()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(30); // still 30 days out
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        await PermanentlyDeleteAccountAfterGracePeriodHandler.Handle(
            new CheckAccountGracePeriodExpired(owner.Id), session, CancellationToken.None);

        owner.IsPermanentlyDeleted.Should().BeFalse();
        await session.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_WhenGracePeriodHasElapsed_PermanentlyDeletesAndPersists()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(-1); // already in the past
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        await PermanentlyDeleteAccountAfterGracePeriodHandler.Handle(
            new CheckAccountGracePeriodExpired(owner.Id), session, CancellationToken.None);

        owner.IsPermanentlyDeleted.Should().BeTrue();
        session.Received(1).Store(Arg.Is<OwnerAccount[]>(arr => arr.Length == 1 && arr[0] == owner));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
