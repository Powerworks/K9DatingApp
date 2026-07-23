using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.ShelterAdoption.Domain;

/// <summary>
/// [PLANNED -> BUILT, first slice] Spec/K9CRUSH.emlang.v3.yaml's
/// VolunteeringAndHomeChecks chapter - a member applying to become an
/// approved volunteer. Distinct from FosterApplication - a volunteer isn't
/// necessarily fostering, and areas of interest span beyond home checks
/// (Transport, Fundraising, Events, Administration, FosterSupport).
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

    /// <summary>The emlang yaml's "Apply To Volunteer" -> "Volunteer
    /// Application Submitted".</summary>
    public static VolunteerApplication Apply(Guid applicantOwnerId, VolunteerAreaOfInterest areaOfInterest)
    {
        return new VolunteerApplication
        {
            ApplicantOwnerId = applicantOwnerId,
            AreaOfInterest = areaOfInterest,
            Status = VolunteerApplicationStatus.Submitted,
            SubmittedAt = DateTimeOffset.UtcNow
        };
    }
}
