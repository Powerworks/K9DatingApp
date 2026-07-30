using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using K9Crush.BuildingBlocks.Persistence;
using K9Crush.BuildingBlocks.Web;
using K9Crush.Modules.Identity.Domain;

namespace K9Crush.Modules.Identity.Api;

/// <summary>
/// Composition root for the Identity module. Api.Host discovers this via
/// assembly scanning (see Program.cs) - nothing else references this type.
/// </summary>
public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public IMartenModuleConfiguration MartenConfiguration { get; } = new IdentityMartenConfiguration();

    // This module now consumes a cross-module integration event
    // (ShelterAccountCreatedV1, via Automations/PromoteOwnerToShelterOnAccountCreated)
    // - needs its own durable listener queue bound to k9crush.events, same
    // pattern as Discovery's "discovery.integration-events" - see IModule.cs.
    public string? IntegrationEventQueueName => "identity.integration-events";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // IQuerySession is scoped (registered by AddMarten in Program.cs),
        // so this must be scoped too - a singleton would capture a
        // disposed session across requests.
        services.AddScoped<IOwnerRoleLookup, MartenOwnerRoleLookup>();
    }

    private sealed class IdentityMartenConfiguration : IMartenModuleConfiguration
    {
        public string SchemaName => "identity";

        public void Configure(StoreOptions options)
        {
            // Event store schema is configured once, centrally, in
            // Program.cs - see its comment for why this can't be a
            // per-module setting (Marten only has one event-store schema
            // per StoreOptions, not one per registered module).

            // ADR-031 (Phase 4/5): OwnerAccount is event-sourced AND
            // registered as its own Inline snapshot - OwnerAccountViewHandler/
            // ViewProfileSettingsHandler genuinely query it by id, and
            // MartenOwnerRoleLookup (ADR-017) reads it on every
            // Admin/Shelter-policy-gated request. Feedback is event-sourced
            // with no snapshot - nothing under ReadModels/** queries it
            // (same as Media's MediaAsset, Phase 1).
            //
            // The Inline snapshot is a normal Marten document under the
            // hood (mt_doc_owneraccount) - it needs the same explicit
            // per-type DatabaseSchemaName() call any other document does,
            // which this didn't have until this fix. Without it, Marten's
            // document schema defaults to Postgres's "public" schema
            // regardless of the module's intended SchemaName.
            options.Projections.Snapshot<OwnerAccount>(JasperFx.Events.Projections.SnapshotLifecycle.Inline);
            options.Schema.For<OwnerAccount>().DatabaseSchemaName(SchemaName);
        }
    }
}
