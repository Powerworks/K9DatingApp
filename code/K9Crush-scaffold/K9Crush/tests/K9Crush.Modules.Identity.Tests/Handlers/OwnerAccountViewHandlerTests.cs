using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Identity.Api.ReadModels.OwnerAccountView;
using K9Crush.Modules.Identity.Domain;
using Xunit;

namespace K9Crush.Modules.Identity.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - OwnerAccountViewHandler only calls
/// IQuerySession.LoadAsync (no Query&lt;T&gt;() LINQ), so mocks cleanly here.
/// </summary>
public class OwnerAccountViewHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenOwnerDoesNotExist_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<OwnerAccount>(ownerId, Arg.Any<CancellationToken>()).Returns((OwnerAccount?)null);

        var result = await OwnerAccountViewHandler.Handle(BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenOwnerExists_ReturnsAccountDetails()
    {
        var (owner, _) = OwnerAccount.CreateNew(Guid.NewGuid(), "owner@example.com", DateTimeOffset.UtcNow);
        owner.MarkVerified();
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<OwnerAccount>(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await OwnerAccountViewHandler.Handle(BuildUser(owner.Id), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<OwnerAccountResponse>>();
        var response = ((Ok<OwnerAccountResponse>)result.Result).Value!;
        response.OwnerId.Should().Be(owner.Id);
        response.Email.Should().Be(owner.Email);
        response.IsVerified.Should().BeTrue();
        response.CreatedAt.Should().Be(owner.CreatedAt);
    }
}
