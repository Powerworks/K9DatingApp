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

    // NotifyOnMatchHandler.Handle(MatchCreatedV1, ...) needs this module's
    // own durable queue bound to k9crush.events, or Discovery's published
    // event is never delivered back into this process - see IModule.cs's
    // doc comment for the fuller writeup (same mechanism Discovery itself
    // uses to receive DogProfileCreatedV1 from Profiles).
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
            options.Schema.For<NotificationPreference>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.OwnerId);

            options.Schema.For<NotificationLog>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id)
                .Index(x => x.OwnerId);

            options.Schema.For<OwnerContact>()
                .DatabaseSchemaName(SchemaName);

            options.Schema.For<NotificationTemplate>()
                .DatabaseSchemaName(SchemaName)
                .Identity(x => x.Id);
        }
    }
}
