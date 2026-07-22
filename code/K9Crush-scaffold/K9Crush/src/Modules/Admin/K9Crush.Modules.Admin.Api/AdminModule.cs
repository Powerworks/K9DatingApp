using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using K9Crush.BuildingBlocks.Persistence;
using K9Crush.BuildingBlocks.Web;
using K9Crush.Modules.Admin.Domain;

namespace K9Crush.Modules.Admin.Api;

/// <summary>
/// Composition root for the Admin module. Api.Host discovers this via
/// assembly scanning (see Program.cs) - nothing else references this
/// type.
///
/// First increment covers the emlang yaml's HandlingGeneralFeedbackSupport
/// chapter only (Feedback Inbox/Detail, Respond, Resolve), fed by
/// Identity's cross-module FeedbackSubmittedV1. Two other Admin-actor
/// chapters exist in the yaml and are deliberately deferred:
/// ModeratingFlaggedContentUserReports needs flagged-content producers
/// that don't exist anywhere yet (Report Content flows across Chat/
/// Media/Places/ActivityFeed are all unbuilt), so its queue would start
/// and stay empty; AdminDashboardLandingView's "Platform Health Snapshot"
/// is an infra-monitoring link-out this codebase has no model for. Both
/// are real, separately-scoped follow-ups, not oversights.
/// </summary>
public sealed class AdminModule : IModule
{
    public string Name => "Admin";

    public IMartenModuleConfiguration MartenConfiguration { get; } = new AdminMartenConfiguration();

    // FeedbackSubmittedProjectorHandler.Handle(FeedbackSubmittedV1, ...)
    // needs this module's own durable queue bound to k9crush.events, same
    // mechanism every other module consuming a cross-module event uses -
    // see IModule.cs's doc comment.
    public string? IntegrationEventQueueName => "admin.integration-events";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Nothing beyond Wolverine's auto-discovered handlers for this
        // module yet.
    }

    private sealed class AdminMartenConfiguration : IMartenModuleConfiguration
    {
        public string SchemaName => "admin";

        public void Configure(StoreOptions options)
        {
            options.Schema.For<FeedbackInboxItem>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.OwnerId);
        }
    }
}
