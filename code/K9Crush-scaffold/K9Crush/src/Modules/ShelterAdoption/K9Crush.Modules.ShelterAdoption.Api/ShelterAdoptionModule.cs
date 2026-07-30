using JasperFx.Events.Projections;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using K9Crush.BuildingBlocks.Persistence;
using K9Crush.BuildingBlocks.Web;
using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api;

/// <summary>
/// Composition root for the Shelter &amp; Adoption module. Api.Host
/// discovers this via assembly scanning (see Program.cs) - nothing else
/// references this type.
/// </summary>
public sealed class ShelterAdoptionModule : IModule
{
    public string Name => "ShelterAdoption";

    public IMartenModuleConfiguration MartenConfiguration { get; } = new ShelterAdoptionMartenConfiguration();

    // First set on this module (ADR-028) - CancelApplicationsForRemovedListingHandler
    // and NotifyApplicantsOfListingChangeHandler react to this module's OWN
    // published events (DogListingRemovedV1/DogListingSignificantlyEditedV1),
    // which still route through the shared k9crush.events exchange same as
    // any cross-module event - there's no separate "local-only" pub/sub in
    // this codebase, so a same-module cascade needs a queue too, same as
    // Discovery/Identity/Notifications. Confirmed safe against Wolverine's
    // actual RabbitMQ transport source: the shared exchange is fanout (every
    // bound queue gets every message), and a message type with no local
    // handler is a graceful no-op (NoHandlerContinuation just acks it), not
    // an error - so this queue also quietly absorbing every other module's
    // events it doesn't handle is expected, not a problem.
    public string? IntegrationEventQueueName => "shelteradoption.integration-events";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Nothing beyond Wolverine's auto-discovered handlers for this
        // module yet.
    }

    private sealed class ShelterAdoptionMartenConfiguration : IMartenModuleConfiguration
    {
        public string SchemaName => "shelteradoption";

        public void Configure(StoreOptions options)
        {
            // ADR-031 event-sourcing retrofit, Phase 5/5 - every entity in
            // this module is now an event stream, self-aggregating via
            // Create/Apply and registered as its own Inline snapshot (dual
            // use for the read models that genuinely query current state -
            // GetShelterDogListings/GetDogListingDetails/GetAdoptionListings/
            // GetPendingApplicationsQueue/GetApplicationStatus/
            // GetDraftApplications/GetSurrenderReviewQueue/
            // GetFosterApplicationsQueue/GetVolunteerApplicationsQueue, plus
            // every ownership-check LoadAsync<ShelterAccount>).
            //
            // Event store schema is configured once, centrally, in
            // Program.cs - see its comment for why. Each Inline snapshot
            // below is still a normal Marten document (mt_doc_*) and needs
            // its own explicit DatabaseSchemaName() call - without it,
            // Marten defaults the document schema to "public" regardless
            // of SchemaName, which is what was actually happening here
            // until this fix (see marten_schema_isolation_bug memory).
            options.Projections.Snapshot<ShelterAccount>(SnapshotLifecycle.Inline);
            options.Projections.Snapshot<DogListing>(SnapshotLifecycle.Inline);
            options.Projections.Snapshot<Application>(SnapshotLifecycle.Inline);
            options.Projections.Snapshot<DogSurrenderRequest>(SnapshotLifecycle.Inline);
            options.Projections.Snapshot<FosterApplication>(SnapshotLifecycle.Inline);
            options.Projections.Snapshot<VolunteerApplication>(SnapshotLifecycle.Inline);

            options.Schema.For<ShelterAccount>().DatabaseSchemaName(SchemaName);
            options.Schema.For<DogListing>().DatabaseSchemaName(SchemaName);
            options.Schema.For<Application>().DatabaseSchemaName(SchemaName);
            options.Schema.For<DogSurrenderRequest>().DatabaseSchemaName(SchemaName);
            options.Schema.For<FosterApplication>().DatabaseSchemaName(SchemaName);
            options.Schema.For<VolunteerApplication>().DatabaseSchemaName(SchemaName);
        }
    }
}
