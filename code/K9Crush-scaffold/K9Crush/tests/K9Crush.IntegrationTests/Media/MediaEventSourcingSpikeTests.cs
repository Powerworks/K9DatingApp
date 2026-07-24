using FluentAssertions;
using Marten;
using K9Crush.Modules.Media.Api.Commands.ReportMedia;
using K9Crush.Modules.Media.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.Media;

/// <summary>
/// ADR-031 Phase 1 spike (see the retrofit plan's Phase 0 item 8): answers,
/// against a real Postgres/Marten store rather than a mock or a guess,
/// exactly what FetchForWriting/AggregateStreamAsync do with a stream id
/// that was never started. This determines every future handler's NotFound
/// branch shape across all 5 retrofit phases - written once here so later
/// phases don't each have to re-derive it.
/// </summary>
[Collection(MediaPostgresCollection.Name)]
public class MediaEventSourcingSpikeTests
{
    private readonly MediaPostgresFixture _fixture;

    public MediaEventSourcingSpikeTests(MediaPostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FetchForWriting_OnANonexistentStream_ReturnsAggregateNullRatherThanThrowing()
    {
        await using var session = _fixture.Store.LightweightSession();

        var stream = await session.Events.FetchForWriting<MediaAsset>(Guid.NewGuid(), CancellationToken.None);

        stream.Aggregate.Should().BeNull();
    }

    [Fact]
    public async Task AggregateStreamAsync_OnANonexistentStream_ReturnsNullRatherThanThrowing()
    {
        await using var session = _fixture.Store.LightweightSession();

        var state = await session.Events.AggregateStreamAsync<ReportMediaState>(Guid.NewGuid(), token: CancellationToken.None);

        state.Should().BeNull();
    }

    [Fact]
    public async Task FetchForWriting_ThenSaveChangesTwiceConcurrently_ThrowsAConcurrencyException()
    {
        var mediaAssetId = Guid.NewGuid();
        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            var (asset, uploaded) = MediaAsset.Upload(Guid.NewGuid(), MediaType.Photo, "https://storage.example/photo.jpg");
            seedSession.Events.StartStream<MediaAsset>(asset.Id, uploaded);
            mediaAssetId = asset.Id;
            await seedSession.SaveChangesAsync();
        }

        await using var sessionA = _fixture.Store.LightweightSession();
        await using var sessionB = _fixture.Store.LightweightSession();

        var streamA = await sessionA.Events.FetchForWriting<MediaAsset>(mediaAssetId, CancellationToken.None);
        var streamB = await sessionB.Events.FetchForWriting<MediaAsset>(mediaAssetId, CancellationToken.None);

        streamA.AppendOne(streamA.Aggregate!.Share(MediaVisibility.Public, []));
        streamB.AppendOne(streamB.Aggregate!.Share(MediaVisibility.FollowersOnly, []));

        await sessionA.SaveChangesAsync();

        // The real thrown type is JasperFx.Events.EventStreamUnexpectedMaxEventIdException
        // (base: JasperFx.ConcurrencyException) - a completely different hierarchy
        // from Marten.Exceptions.ConcurrentUpdateException, confirmed live here.
        var act = async () => await sessionB.SaveChangesAsync();
        await act.Should().ThrowAsync<JasperFx.ConcurrencyException>();
    }

    [Fact]
    public async Task UploadShareRemove_RoundTripsAcrossSeparateSessions()
    {
        Guid mediaAssetId;
        var ownerId = Guid.NewGuid();

        await using (var uploadSession = _fixture.Store.LightweightSession())
        {
            var (asset, uploaded) = MediaAsset.Upload(ownerId, MediaType.Video, "https://storage.example/clip.mp4");
            mediaAssetId = asset.Id;
            uploadSession.Events.StartStream<MediaAsset>(asset.Id, uploaded);
            await uploadSession.SaveChangesAsync();
        }

        await using (var shareSession = _fixture.Store.LightweightSession())
        {
            var stream = await shareSession.Events.FetchForWriting<MediaAsset>(mediaAssetId, CancellationToken.None);
            stream.Aggregate.Should().NotBeNull();
            stream.Aggregate!.OwnerId.Should().Be(ownerId);

            var sharedWith = new[] { Guid.NewGuid() };
            var @event = stream.Aggregate.Share(MediaVisibility.SpecificPeople, sharedWith);
            stream.AppendOne(@event);
            await shareSession.SaveChangesAsync();
        }

        await using (var removeSession = _fixture.Store.LightweightSession())
        {
            var stream = await removeSession.Events.FetchForWriting<MediaAsset>(mediaAssetId, CancellationToken.None);
            stream.Aggregate.Should().NotBeNull();
            stream.Aggregate!.Visibility.Should().Be(MediaVisibility.SpecificPeople);
            stream.Aggregate.IsRemoved.Should().BeFalse();

            stream.AppendOne(stream.Aggregate.Remove());
            await removeSession.SaveChangesAsync();
        }

        await using var finalSession = _fixture.Store.LightweightSession();
        var finalState = await finalSession.Events.AggregateStreamAsync<MediaAsset>(mediaAssetId, token: CancellationToken.None);
        finalState.Should().NotBeNull();
        finalState!.IsRemoved.Should().BeTrue();
        finalState.Visibility.Should().Be(MediaVisibility.SpecificPeople);
    }
}
