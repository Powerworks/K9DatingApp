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
/// Covers the emlang yaml's UploadShareRemovePhotosAndVideos chapter:
/// Upload/Share/Remove Media, plus Report Media -> Content Flagged.
///
/// ADR-031 (Phase 1/5, this module's own retrofit): MediaAsset is now
/// event-sourced - no Schema.For&lt;T&gt; document registration, since Marten
/// discovers the event stream from FetchForWriting/StartStream/
/// AggregateStreamAsync calls at runtime. No Inline snapshot is registered
/// either - nothing under ReadModels/** queries MediaAsset today, so
/// there's no read side to persist yet (see MediaAsset.cs's own doc
/// comment for how to add one later if that changes).
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
            options.Events.DatabaseSchemaName = SchemaName;
        }
    }
}
