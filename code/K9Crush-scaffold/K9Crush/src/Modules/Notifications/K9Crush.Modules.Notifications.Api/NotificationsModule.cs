using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using K9Crush.BuildingBlocks.Persistence;
using K9Crush.BuildingBlocks.Web;
using K9Crush.Modules.Notifications.Api.Infrastructure;
using K9Crush.Modules.Notifications.Domain;

namespace K9Crush.Modules.Notifications.Api;

/// <summary>
/// Composition root for the Notifications module. Api.Host discovers this
/// via assembly scanning (see Program.cs) - nothing else references this
/// type.
/// </summary>
public sealed class NotificationsModule : IModule
{
    public string Name => "Notifications";

    public IMartenModuleConfiguration MartenConfiguration { get; } = new NotificationsMartenConfiguration();

    // NotifyOnApplicationRejectedHandler/NotifyOnApplicationApprovedHandler
    // need this module's own durable queue bound to k9crush.events, or
    // ShelterAdoption's published events are never delivered back into
    // this process - see IModule.cs's doc comment for the fuller writeup.
    public string? IntegrationEventQueueName => "notifications.integration-events";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISmtpNotificationSender, MailKitSmtpNotificationSender>();
    }

    private sealed class NotificationsMartenConfiguration : IMartenModuleConfiguration
    {
        public string SchemaName => "notifications";

        public void Configure(StoreOptions options)
        {
            options.Events.DatabaseSchemaName = SchemaName;

            // ADR-031 (Phase 3/5): NotificationPreference and
            // NotificationTemplate are event-sourced AND registered as
            // their own Inline snapshots - both are genuinely queried by
            // a ReadModels/** handler (ViewNotificationPreferences/
            // ViewNotificationTemplates). NotificationLog is event-sourced
            // with no snapshot at all (no query consumer exists, same as
            // Media's MediaAsset in Phase 1).
            options.Projections.Snapshot<NotificationPreference>(JasperFx.Events.Projections.SnapshotLifecycle.Inline);
            options.Projections.Snapshot<NotificationTemplate>(JasperFx.Events.Projections.SnapshotLifecycle.Inline);

            // OwnerContact deliberately stays a plain document, not
            // event-sourced - it's a pure cross-module denormalized cache
            // (Identity's OwnerRegisteredV1 projected into "current email
            // for this owner"), no domain transitions of its own to
            // capture as events, same class of judgment call as ADR-031's
            // per-entity carve-outs elsewhere in this phase.
            options.Schema.For<OwnerContact>()
                .DatabaseSchemaName(SchemaName);
        }
    }
}
