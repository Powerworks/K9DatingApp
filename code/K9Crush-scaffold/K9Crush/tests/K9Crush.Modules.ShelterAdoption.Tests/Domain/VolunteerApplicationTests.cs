using FluentAssertions;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Domain;

/// <summary>
/// Layer 1 (TestingApproach.md) - pure unit tests of the
/// VolunteerApplication entity's factory/domain methods. No mocks, no
/// infra - same scope discipline as FosterApplicationTests.
/// </summary>
public class VolunteerApplicationTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();

    private static VolunteerApplication BuildApplication() =>
        VolunteerApplication.Apply(ApplicantOwnerId, VolunteerAreaOfInterest.HomeChecks);

    [Fact]
    public void Apply_WhenCalled_SetsFieldsAndStatusToSubmitted()
    {
        var before = DateTimeOffset.UtcNow;

        var application = BuildApplication();

        var after = DateTimeOffset.UtcNow;

        application.ApplicantOwnerId.Should().Be(ApplicantOwnerId);
        application.AreaOfInterest.Should().Be(VolunteerAreaOfInterest.HomeChecks);
        application.Status.Should().Be(VolunteerApplicationStatus.Submitted);
        application.SubmittedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Review_WhenCalled_SetsStatusToUnderReview()
    {
        var application = BuildApplication();

        application.Review();

        application.Status.Should().Be(VolunteerApplicationStatus.UnderReview);
    }
}
