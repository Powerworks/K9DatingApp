using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using K9Crush.BuildingBlocks.Persistence;
using K9Crush.BuildingBlocks.Web;
using K9Crush.Modules.Discovery.Domain;

namespace K9Crush.Modules.Discovery.Api;

public sealed class DiscoveryModule : IModule
{
    public string Name => "Discovery";

    public IMartenModuleConfiguration MartenConfiguration { get; } = new DiscoveryMartenConfiguration();

    // DogProfileCreatedProjector.Handle(DogProfileCreatedV1, ...) needs
    // this module's own durable queue bound to k9crush.events, or
    // Profiles' published event is never delivered back into this
    // process - see IModule.cs's doc comment for the fuller writeup.
    public string? IntegrationEventQueueName => "discovery.integration-events";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    private sealed class DiscoveryMartenConfiguration : IMartenModuleConfiguration
    {
        public string SchemaName => "discovery";

        public void Configure(StoreOptions options)
        {
            // Event store side: just the schema for the event stream.
            // Per ADR-019, no Projections.Snapshot<T>() is registered here
            // for command-validation purposes - DetectMutualMatchState
            // (Automations/DetectMutualMatch) is computed live via
            // AggregateStreamAsync<T> per invocation, not persisted as a
            // shared snapshot. This module used to register
            // Projections.Snapshot<MatchAggregate>() here; that bundled
            // type has been removed (see MatchStream.cs's doc comment).
            options.Events.DatabaseSchemaName = SchemaName;

            // NOTE: DogLiked event forwarding to the DetectMutualMatch
            // automation (Automations/DetectMutualMatch) is wired at the
            // AddMarten().IntegrateWithWolverine(...) call site in
            // Api.Host/Program.cs, not here - StoreOptions doesn't own
            // Wolverine's subscription registration.

            // Read-model side (a genuine Query Read Model, unaffected by
            // ADR-019): the feed projection is a plain document under the
            // same schema, kept current by an async projection (registered
            // in Program.cs alongside the integration event consumer that
            // feeds it from DogProfileCreatedV1).
            options.Schema.For<DiscoveryFeedItem>()
                .DatabaseSchemaName(SchemaName)
                .Index(x => x.OwnerId);
        }
    }
}
