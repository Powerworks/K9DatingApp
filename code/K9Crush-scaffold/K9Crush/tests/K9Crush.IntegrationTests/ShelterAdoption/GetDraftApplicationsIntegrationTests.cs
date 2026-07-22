using System.Security.Claims;
using FluentAssertions;
using K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetDraftApplications;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.ShelterAdoption;

/// <summary>
/// Layer 3 (TestingApproach.md) - GetDraftApplicationsHandler calls
/// session.Query&lt;Application&gt;().Where(...).ToListAsync() plus a
/// per-draft LoadAsync&lt;DogListing&gt;() to resolve DogName, the LINQ path
/// Layer 2's IQuerySession mocks can't reach. Scoped to a per-test random
/// applicantOwnerId (from the caller's own JWT), so safe to share
/// ShelterAdoptionPostgresFixture.
/// </summary>
[Collection(ShelterAdoptionPostgresCollection.Name)]
public class GetDraftApplicationsIntegrationTests(ShelterAdoptionPostgresFixture fixture)
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenCallerHasNoDrafts_ReturnsEmptyList()
    {
        await using var session = fixture.Store.LightweightSession();

        var response = await GetDraftApplicationsHandler.Handle(BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        response.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheCallersOwnDraftsWithResolvedDogName()
    {
        var applicantOwnerId = Guid.NewGuid();
        var shelterAccountId = Guid.NewGuid();
        var dogListing = DogListing.Create(shelterAccountId, "Biscuit", "Labrador", 36, "Friendly");
        var ownDraft = Application.StartDraft(applicantOwnerId, dogListing.Id, shelterAccountId);
        var submittedApplication = Application.Submit(applicantOwnerId, dogListing.Id, shelterAccountId, TestIntake.Default); // not a Draft - must be excluded
        var otherOwnersDraft = Application.StartDraft(Guid.NewGuid(), dogListing.Id, shelterAccountId); // different owner - must be excluded

        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Store(dogListing);
            seedSession.Store(ownDraft, submittedApplication, otherOwnersDraft);
            await seedSession.SaveChangesAsync();
        }

        await using var session = fixture.Store.LightweightSession();
        var response = await GetDraftApplicationsHandler.Handle(BuildUser(applicantOwnerId), session, CancellationToken.None);

        response.Items.Should().ContainSingle();
        var item = response.Items.Single();
        item.ApplicationId.Should().Be(ownDraft.Id);
        item.DogName.Should().Be("Biscuit");
    }

    [Fact]
    public async Task Handle_WhenTheDraftsDogListingWasRemoved_ReturnsPlaceholderDogName()
    {
        var applicantOwnerId = Guid.NewGuid();
        var removedDogListingId = Guid.NewGuid();
        var draft = Application.StartDraft(applicantOwnerId, removedDogListingId, Guid.NewGuid());

        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Store(draft); // no DogListing document exists for removedDogListingId
            await seedSession.SaveChangesAsync();
        }

        await using var session = fixture.Store.LightweightSession();
        var response = await GetDraftApplicationsHandler.Handle(BuildUser(applicantOwnerId), session, CancellationToken.None);

        response.Items.Should().ContainSingle().Which.DogName.Should().Be("(listing removed)");
    }
}
