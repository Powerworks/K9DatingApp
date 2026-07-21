using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using K9Crush.BuildingBlocks.Persistence;
using K9Crush.BuildingBlocks.Web;
using K9Crush.Modules.Media.Domain;

namespace K9Crush.Modules.Media.Api;

/// <summary>
/// Composition root for the Media module. Api.Host discovers this via
/// assembly scanning (see Program.cs) - nothing else references this
/// type.
///
/// First increment covers the emlang yaml's UploadShareRemovePhotosAndVideos
/// chapter: Upload/Share/Remove Media, plus Report Media -> Content
/// Flagged (see MediaContentFlaggedV1's own doc comment for why nothing
/// consumes that yet). No IntegrationEventQueueName - this module only
/// ever publishes, it doesn't consume any other module's events yet,
/// same as Profiles.
/// </summary>
public sealed class MediaModule : IModule
{
    public string Name => "Media";

    public IMartenModuleConfiguration MartenConfiguration { get; } = new MediaMartenConfiguration();

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Nothing beyond Wolverine's auto-discovered handlers for this
        // module yet.
    }

    private sealed class MediaMartenConfiguration : IMartenModuleConfiguration
    {
        public string SchemaName => "media";

        public void Configure(StoreOptions options)
        {
            options.Schema.For<MediaAsset>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.OwnerId);
        }
    }
}
