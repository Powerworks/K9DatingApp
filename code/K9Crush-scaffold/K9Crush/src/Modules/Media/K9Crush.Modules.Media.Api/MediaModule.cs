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
/// event-sourced. No Inline snapshot is registered - nothing under
/// ReadModels/** queries MediaAsset today, so there's no read side to
/// persist yet (see MediaAsset.cs's own doc comment for how to add one
/// later if that changes). If one is added, it needs its own
/// options.Schema.For&lt;MediaAsset&gt;().DatabaseSchemaName(SchemaName) call in
/// Configure() below, same as every other module's Inline snapshots -
/// Marten does not infer a document's schema from the module that
/// registered its event stream (see marten_schema_isolation_bug memory).
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
            // Nothing to register here yet - MediaAsset's event stream
            // itself needs no per-module setup (event store schema is
            // configured once, centrally, in Program.cs), and this module
            // has no Inline snapshot to scope. See the class doc comment.
        }
    }
}
