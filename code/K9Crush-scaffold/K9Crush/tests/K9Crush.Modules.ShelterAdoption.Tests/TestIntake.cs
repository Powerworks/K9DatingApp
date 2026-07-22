using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Tests;

/// <summary>
/// Shared sample ApplicationIntake for tests that need a valid Submit()/
/// SubmitDraft() call but don't care about the questionnaire's content -
/// avoids repeating an 18-argument literal across every handler test file
/// that arranges an Application via Application.Submit(...).
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
