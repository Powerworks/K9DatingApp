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
/// Layer 2 (TestingApproach.md) - ReportMediaHandler only calls LoadAsync
/// (no Store/SaveChangesAsync - reporting doesn't mutate the asset
/// itself), so IDocumentSession mocks cleanly here.
/// </summary>
public class ReportMediaHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenAssetDoesNotExist_ReturnsNotFoundAndCascadesNothing()
    {
        var mediaAssetId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<MediaAsset>(mediaAssetId, Arg.Any<CancellationToken>()).Returns((MediaAsset?)null);

        var (result, integrationEvent) = await ReportMediaHandler.Handle(mediaAssetId, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenAssetExists_CascadesContentFlaggedForAnyReporterRegardlessOfOwnership()
    {
        var reporterId = Guid.NewGuid();
        var contentOwnerId = Guid.NewGuid();
        var asset = MediaAsset.Upload(contentOwnerId, MediaType.Photo, "https://storage.example/photo.jpg"); // reporter is NOT the owner

        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<MediaAsset>(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        var (result, integrationEvent) = await ReportMediaHandler.Handle(asset.Id, BuildUser(reporterId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ReportMediaResponse>>();
        integrationEvent.Should().NotBeNull();
        integrationEvent!.MediaAssetId.Should().Be(asset.Id);
        integrationEvent.ContentOwnerId.Should().Be(contentOwnerId);
        integrationEvent.ReporterOwnerId.Should().Be(reporterId);
    }
}
