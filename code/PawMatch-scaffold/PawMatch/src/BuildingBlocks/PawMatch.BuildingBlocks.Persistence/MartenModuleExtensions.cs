using Marten;

namespace PawMatch.BuildingBlocks.Persistence;

/// <summary>
/// Implemented once per module (in that module's Api project) to register
/// its own document types, event types, and projections into the shared
/// Marten StoreOptions - each under its own Postgres schema, per ADR-003
/// (schema-per-module, single database/cluster). This is the *only* place
/// a module talks to Marten configuration directly; slices interact with
/// Marten only through an injected IDocumentSession.
/// </summary>
public interface IMartenModuleConfiguration
{
    /// <summary>
    /// The Postgres schema this module's documents/events live under,
    /// e.g. "profiles", "discovery", "chat". Keeping data schema-isolated
    /// from day one means splitting a module into its own database later
    /// is a connection-string change, not a data migration.
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
