using FluentAssertions;
using K9Crush.Modules.Media.Domain;
using Xunit;

namespace K9Crush.Modules.Media.Tests.Domain;

/// <summary>
/// Layer 1 (TestingApproach.md) - pure unit tests of MediaAsset's factory
/// method and domain methods. No mocks, no infra.
/// </summary>
public class MediaAssetTests
{
    [Fact]
    public void Upload_WhenCalled_CreatesAssetWithNoVisibilityYet()
    {
        var ownerId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var asset = MediaAsset.Upload(ownerId, MediaType.Photo, "https://storage.example/photo.jpg");

        var after = DateTimeOffset.UtcNow;
        asset.OwnerId.Should().Be(ownerId);
        asset.MediaType.Should().Be(MediaType.Photo);
        asset.StorageUrl.Should().Be("https://storage.example/photo.jpg");
        asset.UploadedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        asset.Visibility.Should().BeNull();
        asset.SharedWithOwnerIds.Should().BeEmpty();
        asset.SharedAt.Should().BeNull();
    }

    [Fact]
    public void Share_WhenCalled_SetsVisibilityAndSharedWithOwnerIdsAndSharedAt()
    {
        var asset = MediaAsset.Upload(Guid.NewGuid(), MediaType.Video, "https://storage.example/clip.mp4");
        var sharedWith = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var before = DateTimeOffset.UtcNow;

        asset.Share(MediaVisibility.SpecificPeople, sharedWith);

        var after = DateTimeOffset.UtcNow;
        asset.Visibility.Should().Be(MediaVisibility.SpecificPeople);
        asset.SharedWithOwnerIds.Should().BeEquivalentTo(sharedWith);
        asset.SharedAt.Should().NotBeNull();
        asset.SharedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
