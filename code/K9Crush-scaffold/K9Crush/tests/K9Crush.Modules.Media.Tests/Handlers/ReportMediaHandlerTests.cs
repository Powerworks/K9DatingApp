using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Media.Api.Commands.ReportMedia;
using K9Crush.Modules.Media.Domain;
using Xunit;

namespace K9Crush.Modules.Media.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ReportMediaHandler only calls
/// Events.AggregateStreamAsync (no append at all - reporting doesn't
/// mutate the asset itself), so IDocumentSession mocks cleanly here
/// (ADR-031: AggregateStreamAsync is a genuine IEventStoreOperations
/// interface member, confirmed via reflection).
/// </summary>
public class ReportMediaHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static IDocumentSession BuildSessionWithState(Guid mediaAssetId, ReportMediaState? state)
    {
        var session = Substitute.For<IDocumentSession>();
        var eventStore = Substitute.For<Marten.Events.IEventStoreOperations>();
        session.Events.Returns(eventStore);
        // AggregateStreamAsync<T>(Guid streamId, long version = 0, DateTimeOffset?
        // timestamp = null, T state = default, long fromVersion = 0, CancellationToken
        // token = default) - confirmed via reflection against the installed Marten
        // 9.17.1, not guessed; ReturnsForAnyArgs sidesteps needing to match every
        // optional argument's default exactly.
        eventStore.AggregateStreamAsync<ReportMediaState>(
                Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<DateTimeOffset?>(), Arg.Any<ReportMediaState>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(Task.FromResult(state));
        return session;
    }

    [Fact]
    public async Task Handle_WhenAssetDoesNotExist_ReturnsNotFoundAndCascadesNothing()
    {
        var mediaAssetId = Guid.NewGuid();
        var session = BuildSessionWithState(mediaAssetId, null);

        var (result, integrationEvent) = await ReportMediaHandler.Handle(mediaAssetId, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenAssetExists_CascadesContentFlaggedForAnyReporterRegardlessOfOwnership()
    {
        var reporterId = Guid.NewGuid();
        var contentOwnerId = Guid.NewGuid();
        var mediaAssetId = Guid.NewGuid();
        var (_, uploadedEvent) = MediaAsset.Upload(contentOwnerId, MediaType.Photo, "https://storage.example/photo.jpg"); // reporter is NOT the owner
        var state = ReportMediaState.Create(uploadedEvent);

        var session = BuildSessionWithState(mediaAssetId, state);

        var (result, integrationEvent) = await ReportMediaHandler.Handle(mediaAssetId, BuildUser(reporterId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ReportMediaResponse>>();
        integrationEvent.Should().NotBeNull();
        integrationEvent!.MediaAssetId.Should().Be(mediaAssetId);
        integrationEvent.ContentOwnerId.Should().Be(contentOwnerId);
        integrationEvent.ReporterOwnerId.Should().Be(reporterId);
    }
}
