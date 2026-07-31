using Marten;

namespace K9Crush.BuildingBlocks.Persistence;

/// <summary>
/// Implemented once per module (in that module's Api project) to register
/// its own document types and projections into the shared Marten
/// StoreOptions - each module's documents live under its own Postgres
/// schema, per ADR-003 (schema-per-module, single database/cluster). This
/// is the *only* place a module talks to Marten configuration directly;
/// slices interact with Marten only through an injected IDocumentSession.
///
/// **Documents only, not events.** ADR-003's schema-per-module framing
/// applies to document/projection tables (mt_doc_*), which Marten does
/// support scoping per type via options.Schema.For&lt;T&gt;().DatabaseSchemaName().
/// The event store (mt_events/mt_streams) is a single shared schema for
/// the whole StoreOptions - Marten has no per-module event schema
/// mechanism - configured once, centrally, in Api.Host's Program.cs, not
/// here. A module's Configure() should never set options.Events.
/// DatabaseSchemaName itself (see marten_schema_isolation_bug memory for
/// what happened when every module tried to).
/// </summary>
public interface IMartenModuleConfiguration
{
    /// <summary>
    /// The Postgres schema this module's documents live under, e.g.
    /// "identity", "shelteradoption", "notifications". Keeping document
    /// data schema-isolated from day one means splitting a module into
    /// its own database later is a connection-string change, not a data
    /// migration. Does not apply to the event store - see the interface
    /// doc comment.
    /// </summary>
    string SchemaName { get; }

    void Configure(StoreOptions options);
}

public static class MartenModuleExtensions
{
    /// <summary>
    /// Applies every registered module's Marten configuration to a single
    /// shared StoreOptions instance. Called once from Api.Host composition.
    /// </summary>
    public static void ApplyModuleConfigurations(
        this StoreOptions options,
        IEnumerable<IMartenModuleConfiguration> moduleConfigurations)
    {
        foreach (var module in moduleConfigurations)
        {
            module.Configure(options);
        }
    }
}
