using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.ShelterAdoption.Domain;

/// <summary>
/// [PLANNED -> BUILT] Spec/K9CRUSH.emlang.v3.yaml's FosteringADog chapter
/// - a member applying to become an approved foster caregiver. Distinct
/// from Application (adopting) and DogSurrenderRequest (surrendering) -
/// this is about becoming eligible to foster at all, before any specific
/// dog is involved. HomeType is Application's own enum, reused directly -
/// same real-world question, no reason to duplicate it.
/// </summary>
public enum FosterApplicationStatus
{
    Submitted,
    UnderReview,
    Approved,
    Rejected
}

public class FosterApplication : Entity
{
    [JsonInclude] public Guid ApplicantOwnerId { get; private set; }
    [JsonInclude] public HomeType HomeType { get; private set; }
    [JsonInclude] public bool HasGarden { get; private set; }
    [JsonInclude] public bool HasOtherPets { get; private set; }
    [JsonInclude] public DateOnly AvailableFrom { get; private set; }
    [JsonInclude] public FosterApplicationStatus Status { get; private set; }
    [JsonInclude] public string? RejectionReason { get; private set; }
    [JsonInclude] public DateTimeOffset SubmittedAt { get; private set; }

    [JsonConstructor]
    private FosterApplication() { }

    /// <summary>The emlang yaml's "Apply To Foster" -> "Foster Application
    /// Submitted".</summary>
    public static FosterApplication Apply(
        Guid applicantOwnerId, HomeType homeType, bool hasGarden, bool hasOtherPets, DateOnly availableFrom)
    {
        return new FosterApplication
        {
            ApplicantOwnerId = applicantOwnerId,
            HomeType = homeType,
            HasGarden = hasGarden,
            HasOtherPets = hasOtherPets,
            AvailableFrom = availableFrom,
            Status = FosterApplicationStatus.Submitted,
            SubmittedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>The emlang yaml's "Review Foster Application" -> "Foster
    /// Application Reviewed". State-guard (only valid from Submitted)
    /// lives in the handler.</summary>
    public void Review() => Status = FosterApplicationStatus.UnderReview;

    /// <summary>The emlang yaml's "Approve Foster Caregiver" -> "Foster
    /// Caregiver Approved". State-guard (only valid from UnderReview)
    /// lives in the handler.</summary>
    public void Approve() => Status = FosterApplicationStatus.Approved;

    /// <summary>The emlang yaml's "Reject Foster Application" -> "Foster
    /// Application Rejected". State-guard (only valid from UnderReview)
    /// lives in the handler.</summary>
    public void Reject(string reason)
    {
        RejectionReason = reason.Trim();
        Status = FosterApplicationStatus.Rejected;
    }
}
