using FluentAssertions;
using K9Crush.Modules.Chat.Api.Automations.CreateConversationOnMatch;
using K9Crush.Modules.Chat.Domain.Events;
using K9Crush.Modules.Discovery.Contracts;
using Xunit;

namespace K9Crush.IntegrationTests.Chat;

/// <summary>
/// Layer 3 (TestingApproach.md) - CreateConversationOnMatchHandler needs
/// a real event store (AggregateStreamAsync).
///
/// Own dedicated container per test class (IAsyncLifetime), not the usual
/// shared ChatPostgresCollection fixture - confirmed live that Chat's
/// tests hit some cross-test-class interaction when sharing one Marten
/// DocumentStore/container across multiple test classes that each use
/// AggregateStreamAsync&lt;T&gt; with their own distinct state types
/// (LoadAsync calls in unrelated test classes started failing with
/// "could not determine an id/Id field" for a state type that was never
/// meant to be a document). Root cause not fully isolated - same
/// pragmatic per-test-container fix already applied to
/// BootstrapAdminIntegrationTests/ViewNotificationTemplatesIntegrationTests
/// for a different but same-shaped isolation problem.
/// </summary>
public class CreateConversationOnMatchIntegrationTests : IAsyncLifetime
{
    private readonly ChatPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    private static MatchCreatedV1 BuildMatch(Guid matchId, Guid ownerAId, Guid ownerBId) => new(
        EventId: Guid.NewGuid(), OccurredAt: DateTimeOffset.UtcNow,
        MatchId: matchId, DogAId: Guid.NewGuid(), DogBId: Guid.NewGuid(),
        OwnerAId: ownerAId, OwnerBId: ownerBId);

    [Fact]
    public async Task Handle_CreatesAConversationStreamKeyedByTheMatchId()
    {
        var matchId = Guid.NewGuid();
        var ownerAId = Guid.NewGuid();
        var ownerBId = Guid.NewGuid();

        await using var session = _fixture.Store.LightweightSession();
        await CreateConversationOnMatchHandler.Handle(BuildMatch(matchId, ownerAId, ownerBId), session, CancellationToken.None);

        await using var verifySession = _fixture.Store.LightweightSession();
        var state = await verifySession.Events.AggregateStreamAsync<CreateConversationOnMatchState>(matchId);
        state!.Exists.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenRedelivered_DoesNotCreateADuplicateConversation()
    {
        var matchId = Guid.NewGuid();
        var matchEvent = BuildMatch(matchId, Guid.NewGuid(), Guid.NewGuid());

        await using (var firstSession = _fixture.Store.LightweightSession())
        {
            await CreateConversationOnMatchHandler.Handle(matchEvent, firstSession, CancellationToken.None);
        }

        await using var secondSession = _fixture.Store.LightweightSession();
        await CreateConversationOnMatchHandler.Handle(matchEvent, secondSession, CancellationToken.None);

        await using var verifySession = _fixture.Store.LightweightSession();
        var eventCount = await verifySession.Events.AggregateStreamAsync<ConversationEventCountState>(matchId);
        eventCount!.Count.Should().Be(1, "redelivery must not append a second ConversationCreated");
    }

    /// <summary>
    /// Test-only aggregation state (not production code) - counts events
    /// on the stream via the same AggregateStreamAsync mechanism every
    /// production command state in this codebase uses, rather than
    /// Marten's session.Events.FetchStreamAsync, which isn't used
    /// anywhere else in this codebase and turned out to leave the shared
    /// test container's schema state in a way that broke unrelated
    /// LoadAsync calls in other test classes sharing the same collection
    /// fixture (confirmed live) - AggregateStreamAsync is the
    /// already-proven-safe path.
    /// </summary>
    internal sealed class ConversationEventCountState
    {
        public int Count { get; private set; }
        public void Apply(ConversationCreated e) => Count++;
    }
}
