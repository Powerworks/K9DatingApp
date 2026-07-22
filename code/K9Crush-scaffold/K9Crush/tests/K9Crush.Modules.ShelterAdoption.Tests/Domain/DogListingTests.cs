using FluentAssertions;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Domain;

/// <summary>
/// Layer 1 (TestingApproach.md) - pure unit tests of the DogListing
/// entity's factory/domain methods. No mocks, no infra - same scope
/// discipline as ApplicationTests (no "wrong status" rejection tests,
/// since DogListing's methods don't guard preconditions either).
/// </summary>
public class DogListingTests
{
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    [Fact]
    public void Create_WhenCalled_SetsFieldsAndDefaultsStatusToNotReadyYet()
    {
        var before = DateTimeOffset.UtcNow;

        var dogListing = DogListing.Create(ShelterAccountId, "  Biscuit  ", "  Beagle mix  ", 24, "  Friendly, good with kids  ");

        var after = DateTimeOffset.UtcNow;

        dogListing.ShelterAccountId.Should().Be(ShelterAccountId);
        dogListing.Name.Should().Be("Biscuit");
        dogListing.Breed.Should().Be("Beagle mix");
        dogListing.AgeInMonths.Should().Be(24);
        dogListing.Bio.Should().Be("Friendly, good with kids");
        dogListing.AddedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        dogListing.Status.Should().Be(DogListingStatus.NotReadyYet,
            "v3 ENRICHMENT: a newly-added listing isn't open for applications until a shelter marks it Available");
    }

    [Fact]
    public void Create_WhenNameIsBlank_Throws()
    {
        var act = () => DogListing.Create(ShelterAccountId, "   ", "Beagle mix", 24, "Bio");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Edit_WhenCalled_SetsFieldsTrimmedAndLeavesStatusUnchanged()
    {
        var dogListing = DogListing.Create(ShelterAccountId, "Biscuit", "Beagle mix", 24, "Friendly");
        dogListing.UpdateStatus(DogListingStatus.Available);

        dogListing.Edit("  Biscuit II  ", "  Beagle  ", 30, "  Still friendly  ");

        dogListing.Name.Should().Be("Biscuit II");
        dogListing.Breed.Should().Be("Beagle");
        dogListing.AgeInMonths.Should().Be(30);
        dogListing.Bio.Should().Be("Still friendly");
        dogListing.Status.Should().Be(DogListingStatus.Available, "editing listing details is unrelated to its status");
    }

    [Theory]
    [InlineData(DogListingStatus.Available)]
    [InlineData(DogListingStatus.NotReadyYet)]
    [InlineData(DogListingStatus.InFoster)]
    [InlineData(DogListingStatus.PendingAdoption)]
    [InlineData(DogListingStatus.Adopted)]
    public void UpdateStatus_WhenCalled_SetsStatusToTheGivenValue(DogListingStatus status)
    {
        var dogListing = DogListing.Create(ShelterAccountId, "Biscuit", "Beagle mix", 24, "Friendly");

        dogListing.UpdateStatus(status);

        dogListing.Status.Should().Be(status);
    }
}
