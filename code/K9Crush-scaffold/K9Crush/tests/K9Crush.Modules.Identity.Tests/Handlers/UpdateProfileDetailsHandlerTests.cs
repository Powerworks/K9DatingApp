using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Identity.Api.Commands.UpdateProfileDetails;
using K9Crush.Modules.Identity.Domain;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - UpdateProfileDetailsHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class UpdateProfileDetailsHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenOwnerDoesNotExist_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(ownerId, Arg.Any<CancellationToken>()).Returns((OwnerAccount?)null);

        var result = await UpdateProfileDetailsHandler.Handle(
            new UpdateProfileDetailsRequest("Alex"), BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenPermanentlyDeleted_ReturnsConflict()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.RequestDeletion();
        owner.ConfirmDeletion(30);
        owner.PermanentlyDelete();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await UpdateProfileDetailsHandler.Handle(
            new UpdateProfileDetailsRequest("Alex"), BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenOwnerExists_UpdatesDisplayNameAndPersists()
    {
        var owner = OwnerAccount.Create(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await UpdateProfileDetailsHandler.Handle(
            new UpdateProfileDetailsRequest("Alex"), BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<UpdateProfileDetailsResponse>>();
        ((Ok<UpdateProfileDetailsResponse>)result.Result).Value!.DisplayName.Should().Be("Alex");
        owner.DisplayName.Should().Be("Alex");
        session.Received(1).Store(Arg.Is<OwnerAccount[]>(arr => arr.Length == 1 && arr[0] == owner));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
