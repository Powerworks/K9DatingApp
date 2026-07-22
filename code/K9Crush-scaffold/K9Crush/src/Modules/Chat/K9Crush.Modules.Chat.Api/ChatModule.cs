using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using K9Crush.BuildingBlocks.Persistence;
using K9Crush.BuildingBlocks.Web;
using K9Crush.Modules.Chat.Domain;

namespace K9Crush.Modules.Chat.Api;

/// <summary>
/// Composition root for the Chat module. Api.Host discovers this via
/// assembly scanning (see Program.cs) - nothing else references this
/// type. Event-sourced (docs/03-solution-architecture.md Section 2.1 -
/// "full message/read-receipt history is exactly what event sourcing is
/// for"), same shape as Discovery.
/// </summary>
public sealed class ChatModule : IModule
{
    public string Name => "Chat";

    public IMartenModuleConfiguration MartenConfiguration { get; } = new ChatMartenConfiguration();

    // CreateConversationOnMatchHandler.Handle(MatchCreatedV1, ...) needs
    // this module's own durable queue bound to k9crush.events, or
    // Discovery's published event is never delivered back into this
    // process - see IModule.cs's doc comment (same mechanism Discovery
    // itself uses to receive DogProfileCreatedV1 from Profiles).
    public string? IntegrationEventQueueName => "chat.integration-events";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    private sealed class ChatMartenConfiguration : IMartenModuleConfiguration
    {
        public string SchemaName => "chat";

        public void Configure(StoreOptions options)
        {
            // Event store side: just the schema for the event stream. Per
            // ADR-019, no Projections.Snapshot<T>() is registered here for
            // command-validation purposes - SendMessageState/MarkAsReadState
            // are computed live via AggregateStreamAsync<T> per invocation,
            // not persisted as a shared snapshot (same discipline as
            // Discovery's DetectMutualMatchState/UndoLastSwipeState).
            options.Events.DatabaseSchemaName = SchemaName;

            // NOTE: ConversationCreated/MessageSent event forwarding to
            // their respective projectors (Automations vs ReadModels here
            // are both same-module Marten-forwarded subscriptions) is
            // wired at the AddMarten().IntegrateWithWolverine(...) call
            // site in Api.Host/Program.cs, not here - StoreOptions doesn't
            // own Wolverine's subscription registration.

            // Read-model side (genuine Query Read Models, unaffected by
            // ADR-019): plain documents under the same schema, kept
            // current by the async projectors in ReadModels/Projectors.
            options.Schema.For<ConversationSummary>()
                .DatabaseSchemaName(SchemaName)
                .Index(x => x.OwnerAId)
                .Index(x => x.OwnerBId);

            options.Schema.For<ChatMessageView>()
                .DatabaseSchemaName(SchemaName)
                .Index(x => x.ConversationId);
        }
    }
}
