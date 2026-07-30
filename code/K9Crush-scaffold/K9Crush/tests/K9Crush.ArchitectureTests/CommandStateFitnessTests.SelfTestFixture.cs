using Marten;

namespace K9Crush.ArchitectureTests.SelfTestFixture
{
    /// <summary>Fixture entity for CommandStateFitnessTests's own self-test - not part of any real module.</summary>
    internal class SelfTestSnapshotEntity
    {
        public Guid Id { get; set; }
    }
}

namespace K9Crush.ArchitectureTests.SelfTestFixture.Commands
{
    /// <summary>Illegitimate shape: LoadAsync against a "snapshot" type from a namespace containing ".Commands" - must be flagged.</summary>
    internal static class FakeCommandHandler
    {
        public static async Task<K9Crush.ArchitectureTests.SelfTestFixture.SelfTestSnapshotEntity?> Handle(
            Guid id, IQuerySession session, CancellationToken ct) =>
            await session.LoadAsync<K9Crush.ArchitectureTests.SelfTestFixture.SelfTestSnapshotEntity>(id, ct);
    }
}

namespace K9Crush.ArchitectureTests.SelfTestFixture.Automations
{
    /// <summary>Legitimate shape: FetchForWriting against the same type - must not be flagged.</summary>
    internal static class FakeAutomationHandler
    {
        public static async Task Handle(Guid id, IDocumentSession session, CancellationToken ct)
        {
            var stream = await session.Events.FetchForWriting<K9Crush.ArchitectureTests.SelfTestFixture.SelfTestSnapshotEntity>(id, ct);
            _ = stream.Aggregate;
        }
    }
}
