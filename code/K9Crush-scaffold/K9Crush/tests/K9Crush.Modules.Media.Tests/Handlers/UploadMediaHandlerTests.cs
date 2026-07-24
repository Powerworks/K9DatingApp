using System.Security.Claims;
using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.Media.Api.Commands.UploadMedia;
using K9Crush.Modules.Media.Domain;
using K9Crush.Modules.Media.Domain.Events;
using Xunit;

namespace K9Crush.Modules.Media.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - UploadMediaHandler only calls
/// Events.StartStream/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here (ADR-031). The "invalid file" rejection branch lives entirely in
/// UploadMediaRequest's IValidatableObject (see that file's doc comment) -
/// Wolverine.Http's own pipeline enforces it before Handle ever runs, so
/// it isn't something this layer's direct Handle(...) calls can exercise.
/// </summary>
public class UploadMediaHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenCalled_StartsStreamAndReturnsIt()
    {
        var ownerId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        var eventStore = Substitute.For<Marten.Events.IEventStoreOperations>();
        session.Events.Returns(eventStore);

        var result = await UploadMediaHandler.Handle(
            new UploadMediaRequest(MediaType.Photo, "https://storage.example/photo.jpg"), BuildUser(ownerId), session, CancellationToken.None);

        var response = result.Value!;
        response.MediaAssetId.Should().NotBeEmpty();
        response.MediaType.Should().Be(nameof(MediaType.Photo));

        // StartStream<TAggregate>(Guid id, params object[] events) - confirmed via
        // reflection against the installed Marten 9.17.1 - the params array is what
        // NSubstitute actually sees for verification, not a single typed event.
        // Plain casts, not `is` pattern-matching - Arg.Is's predicate is an
        // Expression<Predicate<T>>, and expression trees can't contain `is` patterns
        // (CS8122).
        eventStore.Received(1).StartStream<MediaAsset>(
            response.MediaAssetId,
            Arg.Is<object[]>(events => events != null && events.Length == 1 && events[0] != null
                && ((MediaAssetUploadedV1)events[0]).OwnerId == ownerId
                && ((MediaAssetUploadedV1)events[0]).StorageUrl == "https://storage.example/photo.jpg"));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
