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
            options.Schema.For<ShelterAccount>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.RequestedByOwnerId);

            options.Schema.For<DogListing>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.ShelterAccountId);

            options.Schema.For<Application>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.ApplicantOwnerId)
                .Index(x => x.DogListingId)
                .Index(x => x.ShelterAccountId);

            options.Schema.For<DogSurrenderRequest>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.RequestedByOwnerId);

            options.Schema.For<FosterApplication>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.ApplicantOwnerId);

            options.Schema.For<VolunteerApplication>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.ApplicantOwnerId);
        }
    }
}
