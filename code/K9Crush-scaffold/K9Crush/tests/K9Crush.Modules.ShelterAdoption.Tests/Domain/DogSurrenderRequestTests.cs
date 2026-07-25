using FluentAssertions;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Domain;

/// <summary>
/// Layer 1 (TestingApproach.md) - pure unit tests of the
/// DogSurrenderRequest entity's factory/domain methods. No mocks, no
/// infra - same scope discipline as ApplicationTests/DogListingTests (no
/// "wrong status" rejection tests, since domain methods here don't guard
/// their own preconditions either).
/// </summary>
public class DogSurrenderRequestTests
{
    private static readonly Guid RequestedByOwnerId = Guid.NewGuid();

    private static DogSurrenderRequest BuildRequest() => DogSurrenderRequest.RequestNew(
        RequestedByOwnerId, "  Cooper  ", "  Terrier mix  ", 48,
        "  Relocating for work  ", "  Gentle, a little shy  ", "  Up to date on vaccinations  ").DogSurrenderRequest;

    [Fact]
    public void Request_WhenCalled_SetsFieldsTrimmedAndStatusToRequested()
    {
        var before = DateTimeOffset.UtcNow;

        var request = BuildRequest();

        var after = DateTimeOffset.UtcNow;

        request.RequestedByOwnerId.Should().Be(RequestedByOwnerId);
        request.DogName.Should().Be("Cooper");
        request.Breed.Should().Be("Terrier mix");
        request.AgeInMonths.Should().Be(48);
        request.ReasonForSurrender.Should().Be("Relocating for work");
        request.TemperamentNotes.Should().Be("Gentle, a little shy");
        request.HealthNotes.Should().Be("Up to date on vaccinations");
        request.Status.Should().Be(SurrenderRequestStatus.Requested);
        request.RequestedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Review_WhenCalled_SetsStatusToUnderReview()
    {
        var request = BuildRequest();

        request.Review();

        request.Status.Should().Be(SurrenderRequestStatus.UnderReview);
    }

    [Fact]
    public void RequestAdditionalDetails_WhenCalled_SetsReasonTrimmedAndStatusToAdditionalDetailsRequested()
    {
        var request = BuildRequest();

        request.RequestAdditionalDetails("  please confirm vaccination records  ");

        request.AdditionalDetailsRequestReason.Should().Be("please confirm vaccination records");
        request.Status.Should().Be(SurrenderRequestStatus.AdditionalDetailsRequested);
    }

    [Fact]
    public void SubmitAdditionalDetails_WhenCalled_SetsStatusBackToUnderReview()
    {
        var request = BuildRequest();
        request.RequestAdditionalDetails("please confirm vaccination records");

        request.SubmitAdditionalDetails();

        request.Status.Should().Be(SurrenderRequestStatus.UnderReview);
    }

    [Fact]
    public void Accept_WhenCalled_SetsStatusToAccepted()
    {
        var request = BuildRequest();

        request.Accept();

        request.Status.Should().Be(SurrenderRequestStatus.Accepted);
    }

    [Fact]
    public void Decline_WhenCalled_SetsReasonTrimmedAndStatusToDeclined()
    {
        var request = BuildRequest();

        request.Decline("  outside current intake capacity  ");

        request.DeclineReason.Should().Be("outside current intake capacity");
        request.Status.Should().Be(SurrenderRequestStatus.Declined);
    }
}
