using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using K9Crush.BuildingBlocks.Persistence;
using K9Crush.BuildingBlocks.Web;
using K9Crush.Modules.Profiles.Domain;

namespace K9Crush.Modules.Profiles.Api;

/// <summary>
/// Composition root for the Profiles module. Api.Host discovers this via
/// assembly scanning (see Program.cs) - nothing else references this type.
/// </summary>
public sealed class ProfilesModule : IModule
{
    public string Name => "Profiles";

    public IMartenModuleConfiguration MartenConfiguration { get; } = new ProfilesMartenConfiguration();

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Nothing beyond Wolverine's auto-discovered handlers for this
        // module yet. Typed HttpClients / module-specific options would
        // be registered here.
    }

    private sealed class ProfilesMartenConfiguration : IMartenModuleConfiguration
    {
        public string SchemaName => "profiles";

        public void Configure(StoreOptions options)
        {
            options.Schema.For<DogProfile>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.OwnerId);
        }
    }
}
