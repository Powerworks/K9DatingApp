using System.Text.Json.Serialization;
using PawMatch.BuildingBlocks.Domain;

namespace PawMatch.Modules.ShelterAdoption.Domain;

/// <summary>
/// Current-state Marten document. A member's application to adopt a
/// specific DogListing - from Spec/K9CRUSH.emlang.yaml's TheWouldBeAdopter/
/// CheckingApplicationStatus/ShelterReviewsApplication chapters.
///
/// Only the statuses actually driven by a built slice exist here (per the
/// yaml's own richer enum: pending/under_review/returned_for_alteration/
/// approved/rejected/stale/closed/withdrawn). Stale/Closed are time-based
/// (staleAfterDays/closesAfterDays) and need a scheduler that doesn't
/// exist anywhere in this codebase yet - not modeled until one does, same
/// "no infra, no slice" call made for ShelterManagingListings' cascading
/// automations.
///
/// ShelterAccountId is denormalized from DogListing.ShelterAccountId at
/// submission time (not looked up fresh on every query) - a listing
/// can't change which shelter owns it after the fact, so this is safe
/// and saves every shelter-side query (GetPendingApplicationsQueue) a
/// join through DogListing.
/// </summary>
public enum ApplicationStatus
{
    Pending,
    UnderReview,
    ReturnedForAlteration,
    Approved,
    Rejected,
    Withdrawn
}

public class Application : Entity
{
    [JsonInclude] public Guid ApplicantOwnerId { get; private set; }
    [JsonInclude] public Guid DogListingId { get; private set; }
    [JsonInclude] public Guid ShelterAccountId { get; private set; }
    [JsonInclude] public ApplicationStatus Status { get; private set; }
    [JsonInclude] public string? AdditionalDetailsRequestReason { get; private set; }
    [JsonInclude] public string? RejectionReason { get; private set; }
    [JsonInclude] public DateTimeOffset SubmittedAt { get; private set; }

    [JsonConstructor]
    private Application() { }

    public static Application Submit(Guid applicantOwnerId, Guid dogListingId, Guid shelterAccountId)
    {
        return new Application
        {
            ApplicantOwnerId = applicantOwnerId,
            DogListingId = dogListingId,
            ShelterAccountId = shelterAccountId,
            Status = ApplicationStatus.Pending,
            SubmittedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>"open" = still occupying one of the applicant's
    /// maxOpenApplications slots and still eligible for shelter review -
    /// used by SubmitApplicationHandler's limit check and the pending
    /// queue's filter.</summary>
    public bool IsOpen => Status is ApplicationStatus.Pending or ApplicationStatus.UnderReview or ApplicationStatus.ReturnedForAlteration;

    public void Withdraw() => Status = ApplicationStatus.Withdrawn;

    /// <summary>The emlang yaml's "Review Application" -> "Application
    /// Reviewed". State-guard (only valid from Pending) lives in the
    /// handler.</summary>
    public void Review() => Status = ApplicationStatus.UnderReview;

    /// <summary>The emlang yaml's "Request Additional Details" ->
    /// "Additional Details Requested". State-guard (only valid from
    /// UnderReview) lives in the handler.</summary>
    public void RequestAdditionalDetails(string reason)
    {
        AdditionalDetailsRequestReason = reason.Trim();
        Status = ApplicationStatus.ReturnedForAlteration;
    }

    /// <summary>The emlang yaml's "Submit Additional Details" ->
    /// "Additional Details Submitted" - the applicant's response to
    /// RequestAdditionalDetails, returning the application to review.
    /// State-guard (only valid from ReturnedForAlteration) lives in the
    /// handler.</summary>
    public void SubmitAdditionalDetails() => Status = ApplicationStatus.UnderReview;

    /// <summary>The emlang yaml's "Reject Application" -> "Application
    /// Rejected". State-guard (only valid from UnderReview) lives in the
    /// handler.</summary>
    public void Reject(string reason)
    {
        RejectionReason = reason.Trim();
        Status = ApplicationStatus.Rejected;
    }

    /// <summary>The emlang yaml's "Approve Application" -> "Application
    /// Approved". State-guard (only valid from UnderReview) lives in the
    /// handler.</summary>
    public void Approve() => Status = ApplicationStatus.Approved;
}
