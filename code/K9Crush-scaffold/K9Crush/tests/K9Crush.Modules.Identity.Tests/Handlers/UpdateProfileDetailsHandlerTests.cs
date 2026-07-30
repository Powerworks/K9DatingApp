using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Identity.Api.Commands.UpdateProfileDetails;
using K9Crush.Modules.Identity.Domain;
using K9Crush.Modules.Identity.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - UpdateProfileDetailsHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class UpdateProfileDetailsHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenOwnerDoesNotExist_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<OwnerAccount>(ownerId, null, out _);

        var result = await UpdateProfileDetailsHandler.Handle(
            new UpdateProfileDetailsRequest("Alex"), BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenPermanentlyDeleted_ReturnsConflict()
    {
        var (owner, _) = OwnerAccount.CreateNew(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(30);
        owner.PermanentlyDelete();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(owner.Id, owner, out _);

        var result = await UpdateProfileDetailsHandler.Handle(
            new UpdateProfileDetailsRequest("Alex"), BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenOwnerExists_UpdatesDisplayNameAndPersists()
    {
        var (owner, _) = OwnerAccount.CreateNew(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(owner.Id, owner, out var stream);

        var result = await UpdateProfileDetailsHandler.Handle(
            new UpdateProfileDetailsRequest("Alex"), BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<UpdateProfileDetailsResponse>>();
        ((Ok<UpdateProfileDetailsResponse>)result.Result).Value!.DisplayName.Should().Be("Alex");
        owner.DisplayName.Should().Be("Alex");
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && ((OwnerAccountDisplayNameUpdatedV1)o).DisplayName == "Alex"));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
