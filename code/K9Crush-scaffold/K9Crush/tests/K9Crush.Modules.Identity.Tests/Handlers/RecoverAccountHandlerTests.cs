using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Identity.Api.Commands.RecoverAccount;
using K9Crush.Modules.Identity.Domain;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RecoverAccountHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class RecoverAccountHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenOwnerDoesNotExist_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(ownerId, Arg.Any<CancellationToken>()).Returns((OwnerAccount?)null);

        var result = await RecoverAccountHandler.Handle(BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenDeletionWasNeverRequested_ReturnsConflict()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await RecoverAccountHandler.Handle(BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenGracePeriodHasAlreadyExpired_ReturnsConflict()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(-1); // already in the past
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await RecoverAccountHandler.Handle(BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenWithinGracePeriod_RecoversAndPersists()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(30);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await RecoverAccountHandler.Handle(BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RecoverAccountResponse>>();
        owner.DeletionRequestedAt.Should().BeNull();
        owner.GracePeriodEndsAt.Should().BeNull();
        session.Received(1).Store(Arg.Is<OwnerAccount[]>(arr => arr != null && arr.Length == 1 && arr[0] == owner));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
