namespace K9Crush.BuildingBlocks.Domain;

/// <summary>
/// Base type for persisted, event-sourced QUERY READ MODELS only (ADR-019)
/// - e.g. a future rich "match status" view or "conversation transcript"
/// projection that a screen actually queries. Marten owns the
/// event-stream persistence; this base class gives such a projection a
/// consistent Id and a place to apply events via Marten's "Apply"
/// convention (a method named Apply(TEvent) per event type).
///
/// DO NOT use this for command-validation state. Per ADR-019, command
/// state is minimal, per-command (named [CommandName]State), and computed
/// LIVE via session.Events.AggregateStreamAsync&lt;TState&gt;(streamId) -
/// never registered as a Projections.Snapshot&lt;T&gt;() and never
/// inheriting from this base class. If you're tempted to make a type that
/// derives from AggregateRoot and then load it inside a command handler
/// to decide whether to accept or reject that command, stop - that's the
/// exact bundled-aggregate-as-command-state anti-pattern this base class
/// used to enable (see Solution Architecture doc Section 2.2 for the
/// worked example of the mistake this project already made and fixed).
/// </summary>
public abstract class AggregateRoot
{
    public Guid Id { get; protected set; }

    /// <summary>
    /// Marten increments this automatically as events are appended;
    /// exposed here so a query read model can be shown with a "last
    /// updated at version N" affordance if useful. Not a command-state
    /// concern - optimistic concurrency for commands is handled by
    /// Marten's stream version check at Events.Append(), independent of
    /// whether any projection type is involved at all.
    /// </summary>
    public int Version { get; set; }
}
