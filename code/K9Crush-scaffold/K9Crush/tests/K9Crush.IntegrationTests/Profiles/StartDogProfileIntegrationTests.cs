using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Profiles.Api.Commands.StartDogProfile;
using Xunit;

namespace K9Crush.IntegrationTests.Profiles;

/// <summary>
/// Layer 3 (TestingApproach.md) - covers StartDogProfileHandler's
/// maxDogProfiles=10 cap (session.Query&lt;DogProfile&gt;().CountAsync)
/// against a real Postgres via Testcontainers - the case Layer 2's
/// IDocumentSession mocks can't reach. Mirrors ShelterAdoption's
/// DraftsIntegrationTests (the analogous maxOpenApplications/
/// maxDraftApplications cap tests).
/// </summary>
[Collection(ProfilesPostgresCollection.Name)]
public class StartDogProfileIntegrationTests(ProfilesPostgresFixture fixture)
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task StartDogProfile_UpToTheLimit_CreatesDraftsThenReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        var user = BuildUser(ownerId);

        for (var i = 0; i < 10; i++)
        {
            await using var session = fixture.Store.LightweightSession();
            var result = await StartDogProfileHandler.Handle(user, session, CancellationToken.None);

            result.Result.Should().BeOfType<Ok<StartDogProfileResponse>>();
        }

        await using (var session = fixture.Store.LightweightSession())
        {
            var result = await StartDogProfileHandler.Handle(user, session, CancellationToken.None);

            result.Result.Should().BeOfType<Conflict<string>>();
        }
    }

    [Fact]
    public async Task StartDogProfile_ForADifferentOwner_IsNotBlockedByAnotherOwnersCount()
    {
        var firstOwnerId = Guid.NewGuid();
        var firstOwner = BuildUser(firstOwnerId);

        for (var i = 0; i < 10; i++)
        {
            await using var session = fixture.Store.LightweightSession();
            await StartDogProfileHandler.Handle(firstOwner, session, CancellationToken.None);
        }

        var secondOwner = BuildUser(Guid.NewGuid());
        await using (var session = fixture.Store.LightweightSession())
        {
            var result = await StartDogProfileHandler.Handle(secondOwner, session, CancellationToken.None);

            result.Result.Should().BeOfType<Ok<StartDogProfileResponse>>();
        }
    }
}
