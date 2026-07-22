using System.Security.Claims;
using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Media.Api.Commands.UploadMedia;
using K9Crush.Modules.Media.Domain;
using Xunit;

namespace K9Crush.Modules.Media.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - UploadMediaHandler only calls Store/
/// SaveChangesAsync, so IDocumentSession mocks cleanly here. The
/// "invalid file" rejection branch lives entirely in UploadMediaRequest's
/// IValidatableObject (see that file's doc comment) - Wolverine.Http's
/// own pipeline enforces it before Handle ever runs, so it isn't
/// something this layer's direct Handle(...) calls can exercise.
/// </summary>
public class UploadMediaHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenCalled_StoresMediaAssetAndReturnsIt()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();

        var result = await UploadMediaHandler.Handle(
            new UploadMediaRequest(MediaType.Photo, "https://storage.example/photo.jpg"), BuildUser(ownerId), session, CancellationToken.None);

        result.Value!.MediaAssetId.Should().NotBeEmpty();
        result.Value.MediaType.Should().Be(nameof(MediaType.Photo));

        session.Received(1).Store(Arg.Is<MediaAsset[]>(arr =>
arr != null &&             arr.Length == 1 && arr[0].OwnerId == ownerId && arr[0].StorageUrl == "https://storage.example/photo.jpg"));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
