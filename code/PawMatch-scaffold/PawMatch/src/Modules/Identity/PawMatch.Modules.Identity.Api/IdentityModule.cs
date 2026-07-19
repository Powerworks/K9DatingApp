using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PawMatch.BuildingBlocks.Persistence;
using PawMatch.BuildingBlocks.Web;
using PawMatch.Modules.Identity.Domain;

namespace PawMatch.Modules.Identity.Api;

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
    // - needs its own durable listener queue bound to pawmatch.events, same
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
            options.Schema.For<OwnerAccount>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id);
        }
    }
}
