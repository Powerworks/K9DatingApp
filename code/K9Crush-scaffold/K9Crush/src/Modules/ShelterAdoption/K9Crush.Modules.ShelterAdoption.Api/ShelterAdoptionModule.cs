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
        }
    }
}
