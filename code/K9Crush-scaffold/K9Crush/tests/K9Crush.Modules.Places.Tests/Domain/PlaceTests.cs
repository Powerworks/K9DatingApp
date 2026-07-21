using FluentAssertions;
using K9Crush.Modules.Places.Domain;
using Xunit;

namespace K9Crush.Modules.Places.Tests.Domain;

/// <summary>Layer 1 (TestingApproach.md) - pure unit test of Place's factory method. No mocks, no infra.</summary>
public class PlaceTests
{
    [Fact]
    public void Create_WhenCalled_CreatesPlaceOwnedByCaller()
    {
        var ownerId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var place = Place.Create(ownerId, " Bark Park ", PlaceType.DogPark);

        var after = DateTimeOffset.UtcNow;
        place.OwnerId.Should().Be(ownerId);
        place.Name.Should().Be("Bark Park");
        place.PlaceType.Should().Be(PlaceType.DogPark);
        place.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNameIsBlank_Throws(string name)
    {
        var act = () => Place.Create(Guid.NewGuid(), name, PlaceType.Restaurant);

        act.Should().Throw<ArgumentException>();
    }
}
