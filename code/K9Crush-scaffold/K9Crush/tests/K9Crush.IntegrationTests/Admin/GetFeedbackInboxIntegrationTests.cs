using FluentAssertions;
using K9Crush.Modules.Admin.Api.ReadModels.GetFeedbackInbox;
using K9Crush.Modules.Admin.Domain;
using Xunit;

namespace K9Crush.IntegrationTests.Admin;

/// <summary>
/// Layer 3 (TestingApproach.md) - GetFeedbackInboxHandler calls
/// session.Query&lt;FeedbackInboxItem&gt;() with no owner/id filter at all
/// (an admin sees every submission) - a genuinely global, unscoped query,
/// the same class of check that forced BootstrapAdminIntegrationTests/
/// ViewNotificationTemplatesIntegrationTests onto their own dedicated
/// per-instance IAsyncLifetime container instead of sharing one via
/// [Collection(...)] - see those test classes' doc comments for the full
/// writeup of why. Same fix applied here up front rather than discovering
/// the cross-test pollution the hard way a fourth time.
/// </summary>
public class GetFeedbackInboxIntegrationTests : IAsyncLifetime
{
    private readonly AdminPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Handle_WhenNoFeedbackExists_ReturnsEmptyList()
    {
        await using var session = _fixture.Store.LightweightSession();

        var response = await GetFeedbackInboxHandler.Handle(session, CancellationToken.None);

        response.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEveryItemNewestFirst()
    {
        var older = FeedbackInboxItem.Create(Guid.NewGuid(), Guid.NewGuid(), "First submission", DateTimeOffset.UtcNow.AddMinutes(-10));
        var newer = FeedbackInboxItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Second submission", DateTimeOffset.UtcNow);

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            seedSession.Store(older, newer);
            await seedSession.SaveChangesAsync();
        }

        await using var session = _fixture.Store.LightweightSession();
        var response = await GetFeedbackInboxHandler.Handle(session, CancellationToken.None);

        response.Items.Should().HaveCount(2);
        response.Items.Select(x => x.FeedbackId).Should().ContainInOrder(newer.Id, older.Id);
    }
}
