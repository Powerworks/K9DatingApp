using FluentAssertions;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Domain;

/// <summary>
/// Layer 1 (TestingApproach.md) - pure unit tests of the
/// VolunteerApplication entity's factory method. No mocks, no infra - same
/// scope discipline as FosterApplicationTests.
/// </summary>
public class VolunteerApplicationTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();

    [Fact]
    public void Apply_WhenCalled_SetsFieldsAndStatusToSubmitted()
    {
        var before = DateTimeOffset.UtcNow;

        var application = VolunteerApplication.Apply(ApplicantOwnerId, VolunteerAreaOfInterest.HomeChecks);

        var after = DateTimeOffset.UtcNow;

        application.ApplicantOwnerId.Should().Be(ApplicantOwnerId);
        application.AreaOfInterest.Should().Be(VolunteerAreaOfInterest.HomeChecks);
        application.Status.Should().Be(VolunteerApplicationStatus.Submitted);
        application.SubmittedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
