using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.ShelterAdoption.Domain.Events;

namespace K9Crush.Modules.ShelterAdoption.Domain;

/// <summary>
/// Spec/K9CRUSH.emlang.v3.yaml's FosteringADog chapter - a member applying
/// to become an approved foster caregiver. Distinct from Application
/// (adopting) and DogSurrenderRequest (surrendering) - this is about
/// becoming eligible to foster at all, before any specific dog is
/// involved. HomeType is Application's own enum, reused directly - same
/// real-world question, no reason to duplicate it.
///
/// Self-aggregating event-sourced entity (ADR-031, Phase 5/5). Registered
/// as its own Inline snapshot - GetFosterApplicationsQueueHandler
/// genuinely queries it.
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

    public static FosterApplication Create(FosterApplicationSubmittedV1 e) => new()
    {
        ApplicantOwnerId = e.ApplicantOwnerId,
        HomeType = e.HomeType,
        HasGarden = e.HasGarden,
        HasOtherPets = e.HasOtherPets,
        AvailableFrom = e.AvailableFrom,
        Status = FosterApplicationStatus.Submitted,
        SubmittedAt = e.SubmittedAt
    };

    public void Apply(FosterApplicationReviewedV1 e) => Status = FosterApplicationStatus.UnderReview;
    public void Apply(FosterCaregiverApprovedV1 e) => Status = FosterApplicationStatus.Approved;

    public void Apply(FosterApplicationRejectedV1 e)
    {
        RejectionReason = e.Reason;
        Status = FosterApplicationStatus.Rejected;
    }

    /// <summary>
    /// The emlang yaml's "Apply To Foster" -> "Foster Application
    /// Submitted". Named ApplyNew, not Apply - see VolunteerApplication.cs's
    /// identical naming note (the original document-store factory's name
    /// collides with Marten's own Apply(TEvent) convention).
    /// </summary>
    public static (FosterApplication FosterApplication, FosterApplicationSubmittedV1 Event) ApplyNew(
        Guid applicantOwnerId, HomeType homeType, bool hasGarden, bool hasOtherPets, DateOnly availableFrom)
    {
        var @event = new FosterApplicationSubmittedV1(applicantOwnerId, homeType, hasGarden, hasOtherPets, availableFrom, DateTimeOffset.UtcNow);
        return (Create(@event), @event);
    }

    /// <summary>The emlang yaml's "Review Foster Application" -> "Foster
    /// Application Reviewed". State-guard (only valid from Submitted)
    /// lives in the handler.</summary>
    public FosterApplicationReviewedV1 Review()
    {
        var @event = new FosterApplicationReviewedV1();
        Apply(@event);
        return @event;
    }

    /// <summary>The emlang yaml's "Approve Foster Caregiver" -> "Foster
    /// Caregiver Approved". State-guard (only valid from UnderReview)
    /// lives in the handler.</summary>
    public FosterCaregiverApprovedV1 Approve()
    {
        var @event = new FosterCaregiverApprovedV1();
        Apply(@event);
        return @event;
    }

    /// <summary>The emlang yaml's "Reject Foster Application" -> "Foster
    /// Application Rejected". State-guard (only valid from UnderReview)
    /// lives in the handler.</summary>
    public FosterApplicationRejectedV1 Reject(string reason)
    {
        var @event = new FosterApplicationRejectedV1(reason.Trim());
        Apply(@event);
        return @event;
    }
}
