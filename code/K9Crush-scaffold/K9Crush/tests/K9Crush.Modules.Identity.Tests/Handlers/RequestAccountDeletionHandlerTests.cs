using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Identity.Api.Commands.RequestAccountDeletion;
using K9Crush.Modules.Identity.Domain;
using K9Crush.Modules.Identity.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RequestAccountDeletionHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class RequestAccountDeletionHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenOwnerDoesNotExist_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<OwnerAccount>(ownerId, null, out _);

        var (result, integrationEvent) = await RequestAccountDeletionHandler.Handle(BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenPermanentlyDeleted_ReturnsConflictAndCascadesNothing()
    {
        var (owner, _) = OwnerAccount.CreateNew(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(30);
        owner.PermanentlyDelete();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(owner.Id, owner, out _);

        var (result, integrationEvent) = await RequestAccountDeletionHandler.Handle(BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenDeletionAlreadyRequested_ReturnsConflictAndCascadesNothing()
    {
        var (owner, _) = OwnerAccount.CreateNew(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(owner.Id, owner, out _);

        var (result, integrationEvent) = await RequestAccountDeletionHandler.Handle(BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenOwnerExists_RequestsDeletionAndCascadesIntegrationEvent()
    {
        var (owner, _) = OwnerAccount.CreateNew(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(owner.Id, owner, out var stream);

        var (result, integrationEvent) = await RequestAccountDeletionHandler.Handle(BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RequestAccountDeletionResponse>>();
        owner.DeletionRequestedAt.Should().NotBeNull();
        integrationEvent.Should().NotBeNull();
        integrationEvent!.OwnerId.Should().Be(owner.Id);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(OwnerAccountDeletionRequestedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
