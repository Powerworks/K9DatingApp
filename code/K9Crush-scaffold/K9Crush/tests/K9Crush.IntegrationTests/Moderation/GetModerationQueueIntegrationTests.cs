using FluentAssertions;
using K9Crush.Modules.Moderation.Api.ReadModels.GetModerationQueue;
using K9Crush.Modules.Moderation.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.Moderation;

/// <summary>
/// Layer 3 (TestingApproach.md) - GetModerationQueueHandler calls
/// session.Query&lt;FlaggedContent&gt;().Where(Status == Open).ToListAsync() -
/// filtered only by status, not by any per-test random id, so this is a
/// genuinely global-ish query the same class of check that forced
/// GetAdoptionListingsIntegrationTests/GetFeedbackInboxIntegrationTests
/// onto their own dedicated per-instance IAsyncLifetime container instead
/// of sharing one via [Collection(...)] - see those test classes' doc
/// comments for the full writeup of why. Same fix applied here up front.
/// </summary>
public class GetModerationQueueIntegrationTests : IAsyncLifetime
{
    private readonly ModerationPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Handle_WhenNoFlagsExist_ReturnsEmptyList()
    {
        await using var session = _fixture.Store.LightweightSession();

        var response = await GetModerationQueueHandler.Handle(session, CancellationToken.None);

        response.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyOpenFlags()
    {
        var openFlag = FlaggedContent.Create(ContentType.Media, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var dismissedFlag = FlaggedContent.Create(ContentType.Media, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        dismissedFlag.Dismiss();

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            seedSession.Store(openFlag, dismissedFlag);
            await seedSession.SaveChangesAsync();
        }

        await using var session = _fixture.Store.LightweightSession();
        var response = await GetModerationQueueHandler.Handle(session, CancellationToken.None);

        response.Items.Should().ContainSingle().Which.FlagId.Should().Be(openFlag.Id);
    }
}
