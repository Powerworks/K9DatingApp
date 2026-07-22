using FluentAssertions;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Domain;

/// <summary>
/// Layer 1 (TestingApproach.md) - pure unit tests of the FosterApplication
/// entity's factory/domain methods. No mocks, no infra - same scope
/// discipline as ApplicationTests/DogSurrenderRequestTests.
/// </summary>
public class FosterApplicationTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly DateOnly AvailableFrom = new(2026, 8, 1);

    private static FosterApplication BuildApplication() =>
        FosterApplication.Apply(ApplicantOwnerId, HomeType.House, hasGarden: true, hasOtherPets: false, AvailableFrom);

    [Fact]
    public void Apply_WhenCalled_SetsFieldsAndStatusToSubmitted()
    {
        var before = DateTimeOffset.UtcNow;

        var application = BuildApplication();

        var after = DateTimeOffset.UtcNow;

        application.ApplicantOwnerId.Should().Be(ApplicantOwnerId);
        application.HomeType.Should().Be(HomeType.House);
        application.HasGarden.Should().BeTrue();
        application.HasOtherPets.Should().BeFalse();
        application.AvailableFrom.Should().Be(AvailableFrom);
        application.Status.Should().Be(FosterApplicationStatus.Submitted);
        application.SubmittedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Review_WhenCalled_SetsStatusToUnderReview()
    {
        var application = BuildApplication();

        application.Review();

        application.Status.Should().Be(FosterApplicationStatus.UnderReview);
    }

    [Fact]
    public void Approve_WhenCalled_SetsStatusToApproved()
    {
        var application = BuildApplication();

        application.Approve();

        application.Status.Should().Be(FosterApplicationStatus.Approved);
    }

    [Fact]
    public void Reject_WhenCalled_SetsReasonTrimmedAndStatusToRejected()
    {
        var application = BuildApplication();

        application.Reject("  home visit could not confirm a secure garden  ");

        application.RejectionReason.Should().Be("home visit could not confirm a secure garden");
        application.Status.Should().Be(FosterApplicationStatus.Rejected);
    }
}
