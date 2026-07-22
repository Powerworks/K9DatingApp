using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Identity.Api.Commands.RequestAccountDeletion;
using K9Crush.Modules.Identity.Domain;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RequestAccountDeletionHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class RequestAccountDeletionHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenOwnerDoesNotExist_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(ownerId, Arg.Any<CancellationToken>()).Returns((OwnerAccount?)null);

        var (result, integrationEvent) = await RequestAccountDeletionHandler.Handle(BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenPermanentlyDeleted_ReturnsConflictAndCascadesNothing()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(30);
        owner.PermanentlyDelete();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var (result, integrationEvent) = await RequestAccountDeletionHandler.Handle(BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenDeletionAlreadyRequested_ReturnsConflictAndCascadesNothing()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var (result, integrationEvent) = await RequestAccountDeletionHandler.Handle(BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenOwnerExists_RequestsDeletionAndCascadesIntegrationEvent()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var (result, integrationEvent) = await RequestAccountDeletionHandler.Handle(BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RequestAccountDeletionResponse>>();
        owner.DeletionRequestedAt.Should().NotBeNull();
        integrationEvent.Should().NotBeNull();
        integrationEvent!.OwnerId.Should().Be(owner.Id);
        session.Received(1).Store(Arg.Is<OwnerAccount[]>(arr => arr != null && arr.Length == 1 && arr[0] == owner));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
