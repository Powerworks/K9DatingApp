using FluentAssertions;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Domain;

/// <summary>
/// Layer 1 (TestingApproach.md) - pure unit tests of the Application entity's
/// factory methods and domain methods. No mocks, no infra: these only prove
/// "calling this method produces this state change." They deliberately do
/// NOT test calling a method from the "wrong" status (e.g. Approve() on a
/// Withdrawn application) - Application's domain methods don't guard their
/// own preconditions in this codebase (see the class's own doc comments,
/// e.g. "State-guard ... lives in the handler"), so there's nothing here to
/// assert would be rejected. That guarantee belongs to the handler tests in
/// ../Handlers instead.
/// </summary>
public class ApplicationTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    [Fact]
    public void Submit_WhenCalled_CreatesPendingApplicationWithMatchingStartedAndSubmittedTimestamps()
    {
        var before = DateTimeOffset.UtcNow;

        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);

        var after = DateTimeOffset.UtcNow;

        application.ApplicantOwnerId.Should().Be(ApplicantOwnerId);
        application.DogListingId.Should().Be(DogListingId);
        application.ShelterAccountId.Should().Be(ShelterAccountId);
        application.Status.Should().Be(ApplicationStatus.Pending);
        application.StartedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        application.SubmittedAt.Should().Be(application.StartedAt);
        application.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void StartDraft_WhenCalled_CreatesDraftApplicationWithNoSubmittedAtAndIsNotOpen()
    {
        var before = DateTimeOffset.UtcNow;

        var application = Application.StartDraft(ApplicantOwnerId, DogListingId, ShelterAccountId);

        var after = DateTimeOffset.UtcNow;

        application.Status.Should().Be(ApplicationStatus.Draft);
        application.StartedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        application.SubmittedAt.Should().BeNull();
        application.IsOpen.Should().BeFalse("a Draft doesn't occupy a real application slot with the shelter yet");
    }

    [Theory]
    [InlineData(ApplicationStatus.Pending, true)]
    [InlineData(ApplicationStatus.UnderReview, true)]
    [InlineData(ApplicationStatus.ReturnedForAlteration, true)]
    [InlineData(ApplicationStatus.Approved, false)]
    [InlineData(ApplicationStatus.Rejected, false)]
    [InlineData(ApplicationStatus.Withdrawn, false)]
    [InlineData(ApplicationStatus.Draft, false)]
    [InlineData(ApplicationStatus.ClosedDogNoLongerAvailable, false)]
    public void IsOpen_ReflectsExactlyTheThreeStatusesThatOccupyAnApplicationSlot(ApplicationStatus status, bool expectedIsOpen)
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);
        SetStatus(application, status);

        application.IsOpen.Should().Be(expectedIsOpen);
    }

    [Fact]
    public void Withdraw_WhenCalled_SetsStatusToWithdrawn()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);

        application.Withdraw();

        application.Status.Should().Be(ApplicationStatus.Withdrawn);
    }

    [Fact]
    public void Review_WhenCalled_SetsStatusToUnderReview()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);

        application.Review();

        application.Status.Should().Be(ApplicationStatus.UnderReview);
    }

    [Fact]
    public void RequestAdditionalDetails_WhenCalled_SetsReasonTrimmedAndStatusToReturnedForAlteration()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);

        application.RequestAdditionalDetails("  please attach a photo of your yard  ");

        application.AdditionalDetailsRequestReason.Should().Be("please attach a photo of your yard");
        application.Status.Should().Be(ApplicationStatus.ReturnedForAlteration);
    }

    [Fact]
    public void SubmitAdditionalDetails_WhenCalled_SetsStatusBackToUnderReview()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);
        application.RequestAdditionalDetails("more info please");

        application.SubmitAdditionalDetails();

        application.Status.Should().Be(ApplicationStatus.UnderReview);
    }

    [Fact]
    public void Reject_WhenCalled_SetsReasonTrimmedAndStatusToRejected()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);

        application.Reject("  not enough yard space  ");

        application.RejectionReason.Should().Be("not enough yard space");
        application.Status.Should().Be(ApplicationStatus.Rejected);
    }

    [Fact]
    public void Approve_WhenCalled_SetsStatusToApproved()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);

        application.Approve();

        application.Status.Should().Be(ApplicationStatus.Approved);
    }

    [Fact]
    public void EditDetails_WhenCalled_SetsDetailsTrimmedAndLastEditedAt()
    {
        var application = Application.StartDraft(ApplicantOwnerId, DogListingId, ShelterAccountId);
        var before = DateTimeOffset.UtcNow;

        application.EditDetails("  we have a fenced yard and two other dogs  ");

        var after = DateTimeOffset.UtcNow;

        application.Details.Should().Be("we have a fenced yard and two other dogs");
        application.LastEditedAt.Should().NotBeNull();
        application.LastEditedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void SubmitDraft_WhenCalled_SetsStatusToPendingAndSetsSubmittedAt()
    {
        var application = Application.StartDraft(ApplicantOwnerId, DogListingId, ShelterAccountId);
        var before = DateTimeOffset.UtcNow;

        application.SubmitDraft();

        var after = DateTimeOffset.UtcNow;

        application.Status.Should().Be(ApplicationStatus.Pending);
        application.SubmittedAt.Should().NotBeNull();
        application.SubmittedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        application.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void CloseDraftDogNoLongerAvailable_WhenCalled_SetsStatusToClosedDogNoLongerAvailable()
    {
        var application = Application.StartDraft(ApplicantOwnerId, DogListingId, ShelterAccountId);

        application.CloseDraftDogNoLongerAvailable();

        application.Status.Should().Be(ApplicationStatus.ClosedDogNoLongerAvailable);
        application.IsOpen.Should().BeFalse();
    }

    /// <summary>
    /// Application's Status setter is private with no public way to jump
    /// straight to an arbitrary status (by design - every real transition
    /// goes through a named domain method), so the IsOpen theory above
    /// reaches through reflection rather than adding a test-only public
    /// setter to production code.
    /// </summary>
    private static void SetStatus(Application application, ApplicationStatus status)
    {
        typeof(Application).GetProperty(nameof(Application.Status))!
            .SetValue(application, status);
    }
}
