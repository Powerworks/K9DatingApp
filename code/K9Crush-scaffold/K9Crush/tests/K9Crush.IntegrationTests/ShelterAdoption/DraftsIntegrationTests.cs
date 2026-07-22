using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Api.Commands.StartDraftApplication;
using K9Crush.Modules.ShelterAdoption.Api.Commands.SubmitApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.ShelterAdoption;

/// <summary>
/// Layer 3 (TestingApproach.md) - covers the drafts feature's LINQ-query
/// paths (StartDraftApplicationHandler's 3-draft limit,
/// SubmitApplicationHandler's draft-graduation branch) against a real
/// Postgres via Testcontainers. Both handlers call
/// session.Query&lt;Application&gt;().Where(...).ToListAsync() to see the
/// applicant's full application history before deciding what to do -
/// exactly the case Layer 2's IDocumentSession mocks can't reach (see
/// TestingApproach.md's "hard limit" note). This is the durable,
/// automated replacement for the one-off curl verification the drafts
/// feature would otherwise only ever have gotten once, by hand.
/// </summary>
[Collection(ShelterAdoptionPostgresCollection.Name)]
public class DraftsIntegrationTests(ShelterAdoptionPostgresFixture fixture)
{
    private static readonly SubmitApplicationRequest TestSubmitApplicationRequest = new(
        HouseholdSize: 3, HomeOwnership.Own, HomeType.House, HasGarden: true, GardenSize.Medium, GardenEnclosed: true,
        HasChildren: false, ChildrenAgeRange: null, HasOtherPets: false, OtherPetsDetails: null, DailyAloneHours: 4,
        HasUpcomingExtendedAbsence: false, PreferredEnergyLevel: EnergyLevelPreference.Medium, DailyExerciseCommitment: "Two 30-minute walks",
        PastDogOwnershipExperience: true, WillingToCareForMedicalNeedsDog: false, WillingToCareForNervousDog: true, DataProcessingConsent: true);

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private async Task<Guid> CreateDogListingAsync(Guid shelterAccountId, string name)
    {
        await using var session = fixture.Store.LightweightSession();
        var dogListing = DogListing.Create(shelterAccountId, name, "Mixed", 12, "A good dog");
        session.Store(dogListing);
        await session.SaveChangesAsync();
        return dogListing.Id;
    }

    [Fact]
    public async Task StartDraftApplication_UpToTheLimit_CreatesDraftsThenReturnsConflict()
    {
        var applicantOwnerId = Guid.NewGuid();
        var shelterAccountId = Guid.NewGuid();
        var user = BuildUser(applicantOwnerId);

        var dogListingIds = new List<Guid>();
        for (var i = 0; i < 4; i++)
            dogListingIds.Add(await CreateDogListingAsync(shelterAccountId, $"Dog {i}"));

        // Fill all 3 draft slots, one per distinct dog.
        for (var i = 0; i < 3; i++)
        {
            await using var session = fixture.Store.LightweightSession();
            var result = await StartDraftApplicationHandler.Handle(
                dogListingIds[i], user, session, CancellationToken.None);

            result.Result.Should().BeOfType<Ok<StartDraftApplicationResponse>>();
            ((Ok<StartDraftApplicationResponse>)result.Result).Value!.WasExisting.Should().BeFalse();
        }

        // A 4th draft, for a dog not already drafted, should be blocked by the limit.
        await using (var session = fixture.Store.LightweightSession())
        {
            var result = await StartDraftApplicationHandler.Handle(
                dogListingIds[3], user, session, CancellationToken.None);

            result.Result.Should().BeOfType<Conflict<string>>();
        }

        // Calling it again for a dog that already has a draft returns the
        // existing one rather than creating a duplicate or erroring.
        await using (var session = fixture.Store.LightweightSession())
        {
            var result = await StartDraftApplicationHandler.Handle(
                dogListingIds[0], user, session, CancellationToken.None);

            result.Result.Should().BeOfType<Ok<StartDraftApplicationResponse>>();
            ((Ok<StartDraftApplicationResponse>)result.Result).Value!.WasExisting.Should().BeTrue();
        }
    }

    [Fact]
    public async Task SubmitApplication_WhenApplicantHasADraftForThisDog_GraduatesItInsteadOfCreatingANewOne()
    {
        var applicantOwnerId = Guid.NewGuid();
        var shelterAccountId = Guid.NewGuid();
        var user = BuildUser(applicantOwnerId);
        var dogListingId = await CreateDogListingAsync(shelterAccountId, "Buddy");

        Guid draftApplicationId;
        await using (var session = fixture.Store.LightweightSession())
        {
            var draftResult = await StartDraftApplicationHandler.Handle(
                dogListingId, user, session, CancellationToken.None);
            draftApplicationId = ((Ok<StartDraftApplicationResponse>)draftResult.Result).Value!.ApplicationId;
        }

        await using (var session = fixture.Store.LightweightSession())
        {
            var submitResult = await SubmitApplicationHandler.Handle(
                dogListingId, TestSubmitApplicationRequest, user, session, CancellationToken.None);

            submitResult.Result.Should().BeOfType<Ok<SubmitApplicationResponse>>();
            var ok = (Ok<SubmitApplicationResponse>)submitResult.Result;
            ok.Value!.WasDuplicate.Should().BeFalse();
            ok.Value.ApplicationId.Should().Be(draftApplicationId, "submitting should graduate the existing draft, not create a second application");
        }

        await using (var session = fixture.Store.LightweightSession())
        {
            var persisted = await session.LoadAsync<Application>(draftApplicationId);
            persisted.Should().NotBeNull();
            persisted!.Status.Should().Be(ApplicationStatus.Pending);
            persisted.SubmittedAt.Should().NotBeNull();
        }
    }
}
