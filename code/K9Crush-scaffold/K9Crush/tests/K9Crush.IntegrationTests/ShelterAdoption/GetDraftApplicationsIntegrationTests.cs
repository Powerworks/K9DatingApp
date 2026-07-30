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
/// ShelterAdoptionPostgresFixture. Seeding now goes through
/// Events.StartStream (ADR-031) rather than session.Store, since both
/// DogListing and Application are event-sourced.
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
        var (dogListing, dogListingAdded) = DogListing.AddNew(shelterAccountId, "Biscuit", "Labrador", 36, "Friendly");
        var (ownDraft, ownDraftStarted) = Application.StartDraftNew(applicantOwnerId, dogListing.Id, shelterAccountId);
        var (submittedApplication, submittedEvent) = Application.SubmitNew(applicantOwnerId, dogListing.Id, shelterAccountId, TestIntake.Default); // not a Draft - must be excluded
        var (otherOwnersDraft, otherDraftStarted) = Application.StartDraftNew(Guid.NewGuid(), dogListing.Id, shelterAccountId); // different owner - must be excluded

        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Events.StartStream<DogListing>(dogListing.Id, dogListingAdded);
            seedSession.Events.StartStream<Application>(ownDraft.Id, ownDraftStarted);
            seedSession.Events.StartStream<Application>(submittedApplication.Id, submittedEvent);
            seedSession.Events.StartStream<Application>(otherOwnersDraft.Id, otherDraftStarted);
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
        var (draft, draftStarted) = Application.StartDraftNew(applicantOwnerId, removedDogListingId, Guid.NewGuid());

        await using (var seedSession = fixture.Store.LightweightSession())
        {
            seedSession.Events.StartStream<Application>(draft.Id, draftStarted); // no DogListing stream exists for removedDogListingId
            await seedSession.SaveChangesAsync();
        }

        await using var session = fixture.Store.LightweightSession();
        var response = await GetDraftApplicationsHandler.Handle(BuildUser(applicantOwnerId), session, CancellationToken.None);

        response.Items.Should().ContainSingle().Which.DogName.Should().Be("(listing removed)");
    }
}
