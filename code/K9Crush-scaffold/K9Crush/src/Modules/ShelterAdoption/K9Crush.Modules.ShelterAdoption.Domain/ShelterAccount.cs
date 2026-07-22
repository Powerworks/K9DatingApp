using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.ShelterAdoption.Domain;

/// <summary>
/// Current-state Marten document (Shelter & Adoption is document-centric,
/// not event-sourced - see Solution Architecture doc Section 2.1).
/// Represents one shelter/rescue org's application to operate on the
/// platform, from Spec/K9CRUSH.emlang.yaml's TheShelterRescueOrgSigningUp
/// chapter.
///
/// Status only covers what's actually been built (Requested, Verified) -
/// VerificationIssuesFound/Created/Rejected get added to the enum when
/// FlagVerificationIssues/CreateShelterAccount/RejectShelterApplication
/// are actually built, same pattern as OwnerAccount not gaining
/// IsVerified until the VerifyEmail slice needed it.
///
/// RequestedByOwnerId is the Identity module's OwnerAccount.Id (the
/// caller's own JWT sub) - the member who submitted this request, same
/// FK-by-convention pattern as DogProfile.OwnerId. No dependency on the
/// deferred ADR-017 role lookup: a shelter account is just a document a
/// member requested, same as any other owned resource.
///
/// Follows the same [JsonConstructor]/[JsonInclude] serialization pattern
/// as every other document-style entity - see DogProfile.cs for the full
/// writeup of why.
/// </summary>
public enum ShelterAccountStatus
{
    Requested,
    VerificationIssuesFound,
    Verified,
    Created,
    Rejected
}

public class ShelterAccount : Entity
{
    [JsonInclude] public Guid RequestedByOwnerId { get; private set; }
    [JsonInclude] public string BusinessDetails { get; private set; } = default!;
    [JsonInclude] public Guid UtilityBillDocumentId { get; private set; }
    [JsonInclude] public ShelterAccountStatus Status { get; private set; }
    [JsonInclude] public string? VerificationIssuesReason { get; private set; }
    [JsonInclude] public string? RejectionReason { get; private set; }
    [JsonInclude] public DateTimeOffset RequestedAt { get; private set; }

    [JsonConstructor]
    private ShelterAccount() { }

    public static ShelterAccount Create(Guid requestedByOwnerId, string businessDetails, Guid utilityBillDocumentId)
    {
        if (string.IsNullOrWhiteSpace(businessDetails))
            throw new ArgumentException("Business details are required.", nameof(businessDetails));

        return new ShelterAccount
        {
            RequestedByOwnerId = requestedByOwnerId,
            BusinessDetails = businessDetails.Trim(),
            UtilityBillDocumentId = utilityBillDocumentId,
            Status = ShelterAccountStatus.Requested,
            RequestedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Covers both "Shelter Verified" (first try) and "Shelter Reverified"
    /// (after fixing flagged issues) from the emlang yaml - same
    /// technical transition, same resulting status, the yaml's two event
    /// names are narrative framing rather than a real distinction (same
    /// consolidation call made for Identity's sign-up fragment).
    /// State-guard (only valid from Requested) lives in the handler, not
    /// here - same split as VerifyOwnerOnSupabaseConfirmationHandler.
    /// </summary>
    public void Verify() => Status = ShelterAccountStatus.Verified;

    /// <summary>
    /// The emlang yaml's "Flag Verification Issues" -> "Verification
    /// Issues Found". Reason has no corresponding prop in the yaml for
    /// this step, but a flag with no explanation of what to fix isn't a
    /// usable feature for the shelter on the other end - added as a
    /// necessary gap-fill, not speculative scope.
    /// State-guard (only valid from Requested) lives in the handler.
    /// </summary>
    public void FlagVerificationIssues(string reason)
    {
        VerificationIssuesReason = reason.Trim();
        Status = ShelterAccountStatus.VerificationIssuesFound;
    }

    /// <summary>
    /// The emlang yaml's "Resubmit Shelter Account Request" ->
    /// "Shelter Account Request Resubmitted". Resets status back to
    /// Requested (not a new status value) rather than a dedicated
    /// "resubmitted" state - deliberately reuses the exact same
    /// pre-condition Verify() already checks, so re-verification after a
    /// resubmission needs zero changes to VerifyShelterHandler's guard.
    /// State-guard (only valid from VerificationIssuesFound) lives in the
    /// handler.
    /// </summary>
    public void Resubmit(string businessDetails, Guid utilityBillDocumentId)
    {
        if (string.IsNullOrWhiteSpace(businessDetails))
            throw new ArgumentException("Business details are required.", nameof(businessDetails));

        BusinessDetails = businessDetails.Trim();
        UtilityBillDocumentId = utilityBillDocumentId;
        VerificationIssuesReason = null;
        Status = ShelterAccountStatus.Requested;
    }

    /// <summary>
    /// The emlang yaml's "Create Shelter Account" event - named Activate,
    /// not Create, to avoid colliding with the static Create() factory
    /// above (which already ran back at RequestShelterAccount time; this
    /// is the ShelterAccount document going operational, not the document
    /// coming into existence).
    ///
    /// Shared by two different commands with two different preconditions,
    /// same technical transition and same resulting status - the yaml
    /// itself converges "Create Shelter Account" (normal path, from
    /// Verified) and "Approve Shelter Account" (admin override, from
    /// VerificationIssuesFound, bypassing re-verification) on this exact
    /// same "Shelter Account Created" event. State-guard lives in each
    /// handler, not here - see CreateShelterAccountHandler and
    /// ApproveShelterAccountHandler.
    /// </summary>
    public void Activate() => Status = ShelterAccountStatus.Created;

    /// <summary>
    /// The emlang yaml's "Reject Shelter Application" -> "Shelter
    /// Application Rejected". Per the yaml's GWT test
    /// (AdminRejectsAFlaggedShelterApplicationWithAReason), only valid
    /// from VerificationIssuesFound - a shelter gets rejected after
    /// issues were flagged and not resolved to satisfaction, not directly
    /// off a fresh request. State-guard lives in the handler.
    /// </summary>
    public void Reject(string reason)
    {
        RejectionReason = reason.Trim();
        Status = ShelterAccountStatus.Rejected;
    }
}
