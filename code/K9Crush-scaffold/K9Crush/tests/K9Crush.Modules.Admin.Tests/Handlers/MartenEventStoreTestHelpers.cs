using JasperFx.Events;
using Marten;
using NSubstitute;

namespace K9Crush.Modules.Admin.Tests.Handlers;

/// <summary>
/// ADR-031: shared NSubstitute setup for event-sourced handler tests -
/// see K9Crush.Modules.Media.Tests' identical helper (Phase 1) for the
/// full rationale (session.Events is Marten.Events.IEventStoreOperations,
/// a genuine interface, confirmed via reflection against the installed
/// Marten 9.17.1).
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
