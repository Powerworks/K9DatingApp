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
            options.Events.DatabaseSchemaName = SchemaName;

            // ADR-031 (Phase 4/5): OwnerAccount is event-sourced AND
            // registered as its own Inline snapshot - OwnerAccountViewHandler/
            // ViewProfileSettingsHandler genuinely query it by id, and
            // MartenOwnerRoleLookup (ADR-017) reads it on every
            // Admin/Shelter-policy-gated request. Feedback is event-sourced
            // with no snapshot - nothing under ReadModels/** queries it
            // (same as Media's MediaAsset, Phase 1).
            options.Projections.Snapshot<OwnerAccount>(JasperFx.Events.Projections.SnapshotLifecycle.Inline);
        }
    }
}
