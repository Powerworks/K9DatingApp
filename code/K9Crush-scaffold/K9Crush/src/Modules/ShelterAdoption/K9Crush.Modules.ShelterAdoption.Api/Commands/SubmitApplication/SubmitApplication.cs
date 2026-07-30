using System.ComponentModel.DataAnnotations;
using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.SubmitApplication;

/// <summary>
/// The request/command for this slice - what the caller sends. v3
/// ENRICHMENT (Spec/K9CRUSH.emlang.v3.yaml's TheWouldBeAdopter chapter):
/// carries the household/lifestyle intake questionnaire, validated by
/// Wolverine.Http's DataAnnotations middleware same as every other command
/// in this module. GardenSize/GardenEnclosed/ChildrenAgeRange/
/// OtherPetsDetails are conditionally required via IValidatableObject
/// rather than plain attributes, since "required" here depends on
/// HasGarden/HasChildren/HasOtherPets.
/// </summary>
public sealed record SubmitApplicationRequest(
    [property: Range(1, 20)] int HouseholdSize,
    HomeOwnership HomeOwnership,
    HomeType HomeType,
    bool HasGarden,
    GardenSize? GardenSize,
    bool? GardenEnclosed,
    bool HasChildren,
    [property: MaxLength(200)] string? ChildrenAgeRange,
    bool HasOtherPets,
    [property: MaxLength(500)] string? OtherPetsDetails,
    [property: Range(0, 24)] int DailyAloneHours,
    bool HasUpcomingExtendedAbsence,
    EnergyLevelPreference PreferredEnergyLevel,
    [property: Required, MaxLength(500)] string DailyExerciseCommitment,
    bool PastDogOwnershipExperience,
    bool WillingToCareForMedicalNeedsDog,
    bool WillingToCareForNervousDog,
    bool DataProcessingConsent) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!DataProcessingConsent)
            yield return new ValidationResult(
                "Data processing consent is required to submit an application.", [nameof(DataProcessingConsent)]);

        if (HasGarden && (GardenSize is null || GardenEnclosed is null))
            yield return new ValidationResult(
                "GardenSize and GardenEnclosed are required when HasGarden is true.", [nameof(GardenSize), nameof(GardenEnclosed)]);

        if (HasChildren && string.IsNullOrWhiteSpace(ChildrenAgeRange))
            yield return new ValidationResult(
                "ChildrenAgeRange is required when HasChildren is true.", [nameof(ChildrenAgeRange)]);

        if (HasOtherPets && string.IsNullOrWhiteSpace(OtherPetsDetails))
            yield return new ValidationResult(
                "OtherPetsDetails is required when HasOtherPets is true.", [nameof(OtherPetsDetails)]);
    }

    public ApplicationIntake ToIntake() => new(
        HouseholdSize, HomeOwnership, HomeType, HasGarden, GardenSize, GardenEnclosed,
        HasChildren, ChildrenAgeRange, HasOtherPets, OtherPetsDetails, DailyAloneHours,
        HasUpcomingExtendedAbsence, PreferredEnergyLevel, DailyExerciseCommitment,
        PastDogOwnershipExperience, WillingToCareForMedicalNeedsDog, WillingToCareForNervousDog,
        DataProcessingConsent);
}

/// <summary>What this slice hands back to the caller. WasDuplicate is
/// true when an open application for this dog already existed and this
/// call was a no-op (the emlang yaml's "Duplicate Submission Ignored"),
/// not an error - 200 either way.</summary>
public sealed record SubmitApplicationResponse(Guid ApplicationId, bool WasDuplicate);
