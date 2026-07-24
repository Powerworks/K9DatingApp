using JasperFx.Events;
using Marten;
using NSubstitute;

namespace K9Crush.Modules.Media.Tests.Handlers;

/// <summary>
/// ADR-031: shared NSubstitute setup for event-sourced handler tests.
/// session.Events is Marten.Events.IEventStoreOperations - a genuine
/// interface (confirmed via reflection against the installed Marten
/// 9.17.1, not assumed), so FetchForWriting/AggregateStreamAsync mock
/// exactly like LoadAsync always has. This is the FetchForWriting side;
/// AggregateStreamAsync is stubbed directly per-test where needed (no
/// IEventStream wrapper involved for that one).
/// </summary>
internal static class MartenEventStoreTestHelpers
{
    public static IDocumentSession BuildSessionWithFetchForWriting<T>(Guid streamId, T? aggregate, out IEventStream<T> stream)
        where T : class
    {
        var session = Substitute.For<IDocumentSession>();
        var eventStore = Substitute.For<Marten.Events.IEventStoreOperations>();
        session.Events.Returns(eventStore);

        stream = Substitute.For<IEventStream<T>>();
        stream.Aggregate.Returns(aggregate);
        eventStore.FetchForWriting<T>(streamId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(stream));

        return session;
    }
}
