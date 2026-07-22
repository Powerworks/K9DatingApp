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
/// Flagged. Now also consumes Moderation's cross-module
/// ContentRemovalRequestedV1 (Automations/RemoveMediaOnContentRemovalRequested) -
/// the other half of the Moderation module's "Remove Content" command,
/// added once Moderation actually needed a module to react to it.
/// </summary>
public sealed class MediaModule : IModule
{
    public string Name => "Media";

    public IMartenModuleConfiguration MartenConfiguration { get; } = new MediaMartenConfiguration();

    // RemoveMediaOnContentRemovalRequestedHandler.Handle(ContentRemovalRequestedV1, ...)
    // needs this module's own durable queue bound to k9crush.events, same
    // mechanism every other module consuming a cross-module event uses -
    // see IModule.cs's doc comment.
    public string? IntegrationEventQueueName => "media.integration-events";

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
