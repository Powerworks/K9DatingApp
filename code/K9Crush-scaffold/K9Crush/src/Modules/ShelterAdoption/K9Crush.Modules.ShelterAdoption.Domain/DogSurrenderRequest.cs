using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.ShelterAdoption.Domain;

/// <summary>
/// [PLANNED -> BUILT] Spec/K9CRUSH.emlang.v3.yaml's SurrenderingYourDog
/// chapter - a member surrendering their OWN dog into a shelter's care,
/// distinct from Application (an applicant applying to ADOPT a shelter's
/// existing listing).
/// </summary>
public enum SurrenderRequestStatus
{
    Requested,
    UnderReview,
    AdditionalDetailsRequested,
    Accepted,
    Declined
}

public class DogSurrenderRequest : Entity
{
    [JsonInclude] public Guid RequestedByOwnerId { get; private set; }
    [JsonInclude] public string DogName { get; private set; } = default!;
    [JsonInclude] public string Breed { get; private set; } = default!;
    [JsonInclude] public int AgeInMonths { get; private set; }
    [JsonInclude] public string ReasonForSurrender { get; private set; } = default!;
    [JsonInclude] public string TemperamentNotes { get; private set; } = default!;
    [JsonInclude] public string HealthNotes { get; private set; } = default!;
    [JsonInclude] public SurrenderRequestStatus Status { get; private set; }
    [JsonInclude] public string? AdditionalDetailsRequestReason { get; private set; }
    [JsonInclude] public string? DeclineReason { get; private set; }
    [JsonInclude] public DateTimeOffset RequestedAt { get; private set; }

    [JsonConstructor]
    private DogSurrenderRequest() { }

    /// <summary>The emlang yaml's "Request Dog Surrender" -> "Dog
    /// Surrender Requested".</summary>
    public static DogSurrenderRequest Request(
        Guid requestedByOwnerId, string dogName, string breed, int ageInMonths,
        string reasonForSurrender, string temperamentNotes, string healthNotes)
    {
        return new DogSurrenderRequest
        {
            RequestedByOwnerId = requestedByOwnerId,
            DogName = dogName.Trim(),
            Breed = breed.Trim(),
            AgeInMonths = ageInMonths,
            ReasonForSurrender = reasonForSurrender.Trim(),
            TemperamentNotes = temperamentNotes.Trim(),
            HealthNotes = healthNotes.Trim(),
            Status = SurrenderRequestStatus.Requested,
            RequestedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>The emlang yaml's "Review Surrender Request" -> "Surrender
    /// Request Reviewed". State-guard (only valid from Requested) lives
    /// in the handler.</summary>
    public void Review() => Status = SurrenderRequestStatus.UnderReview;

    /// <summary>The emlang yaml's "Request Additional Surrender Details"
    /// -> "Additional Surrender Details Requested". State-guard (only
    /// valid from UnderReview) lives in the handler.</summary>
    public void RequestAdditionalDetails(string reason)
    {
        AdditionalDetailsRequestReason = reason.Trim();
        Status = SurrenderRequestStatus.AdditionalDetailsRequested;
    }

    /// <summary>The emlang yaml's "Submit Additional Surrender Details" ->
    /// "Additional Surrender Details Submitted" - the surrendering
    /// member's response, returning the request to review. No captured
    /// content, same as Application.SubmitAdditionalDetails - the yaml
    /// doesn't specify a form field beyond the reason text already
    /// captured on the request side. State-guard (only valid from
    /// AdditionalDetailsRequested) lives in the handler.</summary>
    public void SubmitAdditionalDetails() => Status = SurrenderRequestStatus.UnderReview;

    /// <summary>The emlang yaml's "Accept Dog Surrender" -> "Dog Surrender
    /// Accepted". State-guard (only valid from UnderReview) lives in the
    /// handler.</summary>
    public void Accept() => Status = SurrenderRequestStatus.Accepted;

    /// <summary>The emlang yaml's "Decline Dog Surrender" -> "Dog
    /// Surrender Declined". State-guard (only valid from UnderReview)
    /// lives in the handler.</summary>
    public void Decline(string reason)
    {
        DeclineReason = reason.Trim();
        Status = SurrenderRequestStatus.Declined;
    }
}
