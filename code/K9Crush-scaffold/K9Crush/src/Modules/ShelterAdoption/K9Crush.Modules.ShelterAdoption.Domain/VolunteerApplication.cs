using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.Modules.ShelterAdoption.Domain.Events;

namespace K9Crush.Modules.ShelterAdoption.Domain;

/// <summary>
/// Spec/K9CRUSH.emlang.v3.yaml's VolunteeringAndHomeChecks chapter - a
/// member applying to become an approved volunteer. Distinct from
/// FosterApplication - a volunteer isn't necessarily fostering, and areas
/// of interest span beyond home checks (Transport, Fundraising, Events,
/// Administration, FosterSupport).
///
/// Self-aggregating event-sourced entity (ADR-031, Phase 5/5). Registered
/// as its own Inline snapshot - GetVolunteerApplicationsQueueHandler
/// genuinely queries it.
/// </summary>
public enum VolunteerAreaOfInterest
{
    Transport,
    Fundraising,
    Events,
    Administration,
    HomeChecks,
    FosterSupport
}

public enum VolunteerApplicationStatus
{
    Submitted,
    UnderReview,
    Approved,
    Rejected
}

public class VolunteerApplication : Entity
{
    [JsonInclude] public Guid ApplicantOwnerId { get; private set; }
    [JsonInclude] public VolunteerAreaOfInterest AreaOfInterest { get; private set; }
    [JsonInclude] public VolunteerApplicationStatus Status { get; private set; }
    [JsonInclude] public DateTimeOffset SubmittedAt { get; private set; }

    [JsonConstructor]
    private VolunteerApplication() { }

    public static VolunteerApplication Create(VolunteerApplicationSubmittedV1 e) => new()
    {
        ApplicantOwnerId = e.ApplicantOwnerId,
        AreaOfInterest = e.AreaOfInterest,
        Status = VolunteerApplicationStatus.Submitted,
        SubmittedAt = e.SubmittedAt
    };

    public void Apply(VolunteerApplicationReviewedV1 e) => Status = VolunteerApplicationStatus.UnderReview;
    public void Apply(VolunteerApprovedV1 e) => Status = VolunteerApplicationStatus.Approved;

    /// <summary>
    /// The emlang yaml's "Apply To Volunteer" -> "Volunteer Application
    /// Submitted". Named ApplyNew, not Apply - the entity's original
    /// document-store factory was named Apply(...) (the domain verb), but
    /// that collides with Marten's own Apply(TEvent) convention method
    /// name used for the instance mutators below, so this retrofit renames
    /// the factory rather than risk confusing the source generator.
    /// </summary>
    public static (VolunteerApplication VolunteerApplication, VolunteerApplicationSubmittedV1 Event) ApplyNew(
        Guid applicantOwnerId, VolunteerAreaOfInterest areaOfInterest)
    {
        var @event = new VolunteerApplicationSubmittedV1(applicantOwnerId, areaOfInterest, DateTimeOffset.UtcNow);
        return (Create(@event), @event);
    }

    /// <summary>The emlang yaml's "Review Volunteer Application" ->
    /// "Volunteer Application Reviewed". State-guard (only valid from
    /// Submitted) lives in the handler.</summary>
    public VolunteerApplicationReviewedV1 Review()
    {
        var @event = new VolunteerApplicationReviewedV1();
        Apply(@event);
        return @event;
    }

    /// <summary>The emlang yaml's "Approve Volunteer" -> "Volunteer
    /// Approved". State-guard (only valid from UnderReview) lives in the
    /// handler.</summary>
    public VolunteerApprovedV1 Approve()
    {
        var @event = new VolunteerApprovedV1();
        Apply(@event);
        return @event;
    }
}
