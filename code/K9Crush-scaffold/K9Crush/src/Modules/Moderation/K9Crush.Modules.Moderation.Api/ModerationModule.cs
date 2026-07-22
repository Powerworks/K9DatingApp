using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using K9Crush.BuildingBlocks.Persistence;
using K9Crush.BuildingBlocks.Web;
using K9Crush.Modules.Moderation.Domain;

namespace K9Crush.Modules.Moderation.Api;

/// <summary>
/// Composition root for the Moderation module. Api.Host discovers this
/// via assembly scanning (see Program.cs) - nothing else references this
/// type.
///
/// Covers the emlang yaml's ModeratingFlaggedContentUserReports chapter:
/// Moderation Queue/Flagged Content Detail views, Dismiss Flag, Remove
/// Content, and the Warn/Suspend/Ban User escalation ladder. Fed today by
/// Media's MediaContentFlaggedV1 only - "Content Flagged" is shared
/// across five yaml chapters but the other four producers (Chat's Report
/// Message, ActivityFeed's Report Post, LeaveAReviewRestaurantOrDogPark's
/// Report Review) don't exist as real slices yet; add a projector for
/// each as its producer module gets built, same "publish now, consumer
/// already exists to receive more producers later" shape as Admin's
/// FeedbackSubmittedV1.
///
/// Deliberately does NOT enforce Suspend/Ban anywhere - see
/// UserModerationRecord's doc comment.
/// </summary>
public sealed class ModerationModule : IModule
{
    public string Name => "Moderation";

    public IMartenModuleConfiguration MartenConfiguration { get; } = new ModerationMartenConfiguration();

    // MediaContentFlaggedProjectorHandler.Handle(MediaContentFlaggedV1, ...)
    // needs this module's own durable queue bound to k9crush.events, same
    // mechanism every other module consuming a cross-module event uses -
    // see IModule.cs's doc comment.
    public string? IntegrationEventQueueName => "moderation.integration-events";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Nothing beyond Wolverine's auto-discovered handlers for this
        // module yet.
    }

    private sealed class ModerationMartenConfiguration : IMartenModuleConfiguration
    {
        public string SchemaName => "moderation";

        public void Configure(StoreOptions options)
        {
            options.Schema.For<FlaggedContent>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.ContentOwnerId);

            options.Schema.For<UserModerationRecord>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id);
        }
    }
}
