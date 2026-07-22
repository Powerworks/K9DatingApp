using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.IntegrationTests.ShelterAdoption;

/// <summary>
/// Shared sample ApplicationIntake for integration tests that need a
/// valid Application.Submit(...) call but don't care about the
/// questionnaire's content - same rationale as the Modules.ShelterAdoption.
/// Tests project's own copy (separate test project, can't share the type
/// directly).
/// </summary>
internal static class TestIntake
{
    internal static readonly ApplicationIntake Default = new(
        HouseholdSize: 3,
        HomeOwnership: HomeOwnership.Own,
        HomeType: HomeType.House,
        HasGarden: true,
        GardenSize: GardenSize.Medium,
        GardenEnclosed: true,
        HasChildren: false,
        ChildrenAgeRange: null,
        HasOtherPets: false,
        OtherPetsDetails: null,
        DailyAloneHours: 4,
        HasUpcomingExtendedAbsence: false,
        PreferredEnergyLevel: EnergyLevelPreference.Medium,
        DailyExerciseCommitment: "Two 30-minute walks",
        PastDogOwnershipExperience: true,
        WillingToCareForMedicalNeedsDog: false,
        WillingToCareForNervousDog: true,
        DataProcessingConsent: true);
}
