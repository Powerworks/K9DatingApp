using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.Identity.Api.Commands.BootstrapAdmin;
using K9Crush.Modules.Identity.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.Identity;

/// <summary>
/// Layer 3 (TestingApproach.md) - BootstrapAdminHandler's "does any Admin
/// already exist" check needs session.Query&lt;OwnerAccount&gt;().AnyAsync()
/// against a real Postgres via Testcontainers - the LINQ path Layer 2's
/// IDocumentSession mocks can't reach.
///
/// Deliberately does NOT share IdentityPostgresFixture via
/// [Collection(...)]/ICollectionFixture the way every other Layer 3 test
/// class in this codebase does. Every other handler's Layer 3 tests scope
/// their assertions to freshly-random ids that never collide across tests
/// sharing one container - this handler's core behavior is a genuinely
/// GLOBAL "does any Admin exist anywhere" check, so one test bootstrapping
/// an Admin would permanently poison every other test's "no Admin exists
/// yet" precondition if they shared a container (confirmed live - this
/// is exactly what happened on the first pass at these tests). Each test
/// method here gets its own fresh container via per-instance
/// IAsyncLifetime instead - xUnit creates a new class instance per [Fact]
/// by default, so this is genuine test isolation, just slower.
/// </summary>
public class BootstrapAdminIntegrationTests : IAsyncLifetime
{
    private readonly IdentityPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private async Task<Guid> SeedOwnerAsync(string email)
    {
        var (owner, @event) = OwnerAccount.CreateNew(Guid.NewGuid(), email, DateTimeOffset.UtcNow);
        await using var session = _fixture.Store.LightweightSession();
        session.Events.StartStream<OwnerAccount>(owner.Id, @event);
        await session.SaveChangesAsync();
        return owner.Id;
    }

    [Fact]
    public async Task Handle_WhenNoAdminExistsYetAndCallerHasAnAccount_PromotesCallerToAdmin()
    {
        var ownerId = await SeedOwnerAsync("first-admin@example.com");

        await using var session = _fixture.Store.LightweightSession();
        var result = await BootstrapAdminHandler.Handle(BuildUser(ownerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<BootstrapAdminResponse>>();
        ((Ok<BootstrapAdminResponse>)result.Result).Value!.Role.Should().Be(nameof(OwnerRole.Admin));

        await using var verifySession = _fixture.Store.LightweightSession();
        var persisted = await verifySession.LoadAsync<OwnerAccount>(ownerId);
        persisted!.Role.Should().Be(OwnerRole.Admin);
    }

    [Fact]
    public async Task Handle_WhenAnAdminAlreadyExists_ReturnsConflictAndDoesNotPromoteTheCaller()
    {
        var firstAdminId = await SeedOwnerAsync("first-admin2@example.com");
        await using (var bootstrapSession = _fixture.Store.LightweightSession())
        {
            await BootstrapAdminHandler.Handle(BuildUser(firstAdminId), bootstrapSession, CancellationToken.None);
        }

        var secondOwnerId = await SeedOwnerAsync("second-owner@example.com");

        await using var session = _fixture.Store.LightweightSession();
        var result = await BootstrapAdminHandler.Handle(BuildUser(secondOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();

        await using var verifySession = _fixture.Store.LightweightSession();
        var persisted = await verifySession.LoadAsync<OwnerAccount>(secondOwnerId);
        persisted!.Role.Should().Be(OwnerRole.Owner, "the conflict path must not promote anyone");
    }

    [Fact]
    public async Task Handle_WhenCallerHasNoOwnerAccountAndNoAdminExistsYet_ReturnsNotFound()
    {
        await using var session = _fixture.Store.LightweightSession();
        var result = await BootstrapAdminHandler.Handle(BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_CalledTwiceByTheSameFirstAdmin_SecondCallIsAlsoBlocked()
    {
        var ownerId = await SeedOwnerAsync("repeat-caller@example.com");

        await using (var firstCallSession = _fixture.Store.LightweightSession())
        {
            var firstResult = await BootstrapAdminHandler.Handle(BuildUser(ownerId), firstCallSession, CancellationToken.None);
            firstResult.Result.Should().BeOfType<Ok<BootstrapAdminResponse>>();
        }

        await using var secondCallSession = _fixture.Store.LightweightSession();
        var secondResult = await BootstrapAdminHandler.Handle(BuildUser(ownerId), secondCallSession, CancellationToken.None);

        secondResult.Result.Should().BeOfType<Conflict<string>>("bootstrap is a one-time action, not idempotent re-promotion");
    }
}
