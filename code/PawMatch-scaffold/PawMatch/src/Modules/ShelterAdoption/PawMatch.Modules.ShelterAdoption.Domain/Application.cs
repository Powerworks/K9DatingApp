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
    Withdrawn,

    // Appended, not inserted above - Marten/System.Text.Json serializes
    // this enum as its integer ordinal (confirmed via OwnerRole in the
    // Identity module), so reordering existing members would silently
    // change what any already-persisted Application document's Status
    // means. Add new members here, always at the end.
    Draft,
    ClosedDogNoLongerAvailable
}

public class Application : Entity
{
    [JsonInclude] public Guid ApplicantOwnerId { get; private set; }
    [JsonInclude] public Guid DogListingId { get; private set; }
    [JsonInclude] public Guid ShelterAccountId { get; private set; }
    [JsonInclude] public ApplicationStatus Status { get; private set; }
    [JsonInclude] public string Details { get; private set; } = string.Empty;
    [JsonInclude] public string? AdditionalDetailsRequestReason { get; private set; }
    [JsonInclude] public string? RejectionReason { get; private set; }
    [JsonInclude] public DateTimeOffset StartedAt { get; private set; }
    [JsonInclude] public DateTimeOffset? SubmittedAt { get; private set; }
    [JsonInclude] public DateTimeOffset? LastEditedAt { get; private set; }

    [JsonConstructor]
    private Application() { }

    public static Application Submit(Guid applicantOwnerId, Guid dogListingId, Guid shelterAccountId)
    {
        var now = DateTimeOffset.UtcNow;
        return new Application
        {
            ApplicantOwnerId = applicantOwnerId,
            DogListingId = dogListingId,
            ShelterAccountId = shelterAccountId,
            Status = ApplicationStatus.Pending,
            StartedAt = now,
            SubmittedAt = now
        };
    }

    /// <summary>
    /// The emlang yaml's ResumingADraftApplication chapter starting point
    /// (and TheWouldBeAdopter's "Gate Application Start", which carries a
    /// draftApplicationId prop) - every application can start life as a
    /// Draft, editable and resumable, before being submitted. Submit()
    /// above remains the express "submit right now, skip the draft
    /// stage" path - both are legitimate, coexisting entry points, not a
    /// replacement of one by the other.
    /// </summary>
    public static Application StartDraft(Guid applicantOwnerId, Guid dogListingId, Guid shelterAccountId)
    {
        return new Application
        {
            ApplicantOwnerId = applicantOwnerId,
            DogListingId = dogListingId,
            ShelterAccountId = shelterAccountId,
            Status = ApplicationStatus.Draft,
            StartedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>"open" = still occupying one of the applicant's
    /// maxOpenApplications slots and still eligible for shelter review -
    /// used by SubmitApplicationHandler's limit check and the pending
    /// queue's filter. Deliberately excludes Draft - the yaml gives
    /// drafts their own separate maxDraftApplications limit, drafts don't
    /// occupy a real application slot with the shelter yet.</summary>
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

    /// <summary>The emlang yaml's "Edit Application Details" ->
    /// "Application Details Edited". State-guard (only valid from Draft)
    /// lives in the handler.</summary>
    public void EditDetails(string details)
    {
        Details = details.Trim();
        LastEditedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Graduates a Draft into a real submitted application - the emlang
    /// yaml's "Submit Application" event, this time carrying a
    /// draftApplicationId prop rather than creating fresh. Shared by
    /// ResumeDraftApplication's eventual submit step and
    /// SubmitApplicationHandler's "I already had a draft going for this
    /// dog" branch - see that handler's comment. State-guard (only valid
    /// from Draft) lives in the handler.
    /// </summary>
    public void SubmitDraft()
    {
        Status = ApplicationStatus.Pending;
        SubmittedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// The emlang yaml's "Check Dog Availability On Resume" ->
    /// "Draft Application Closed: Dog No Longer Available". "No longer
    /// available" is determined by the handler (the DogListing document
    /// no longer existing - RemoveDogListingHandler hard-deletes, see
    /// DogListing.cs), not tracked as a field here. State-guard (only
    /// valid from Draft) lives in the handler.
    /// </summary>
    public void CloseDraftDogNoLongerAvailable() => Status = ApplicationStatus.ClosedDogNoLongerAvailable;
}
