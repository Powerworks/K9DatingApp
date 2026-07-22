using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using K9Crush.BuildingBlocks.Persistence;

namespace K9Crush.BuildingBlocks.Web;

/// <summary>
/// The single contract every business module implements. Api.Host is the
/// only project allowed to know that this list of modules exists - it
/// discovers implementations via assembly scanning and wires each one in
/// at startup. No other project (including other modules) should reference
/// a concrete module's Api project.
/// </summary>
public interface IModule
{
    /// <summary>Short, stable name used in logs, metrics tags, and the Marten schema.</summary>
    string Name { get; }

    /// <summary>
    /// This module's Marten configuration (documents/events/projections
    /// under its own schema). Returns null for modules with no persistence
    /// needs (none currently, but kept nullable for completeness).
    /// </summary>
    IMartenModuleConfiguration MartenConfiguration { get; }

    /// <summary>
    /// Registers the module's own services (handlers are picked up
    /// automatically by Wolverine's assembly scanning - this is for
    /// anything else, e.g. typed HttpClients, module-specific options).
    /// </summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Null (the default) if this module has no local handler for any
    /// cross-module integration event delivered via the k9crush.events
    /// RabbitMQ exchange (Solution Architecture doc Section 5). A module
    /// that DOES consume one (e.g. Discovery's DogProfileCreatedProjector
    /// reacting to Profiles' DogProfileCreatedV1) returns its own durable
    /// queue name here - Api.Host's UseWolverine composition binds it to
    /// the exchange. Without this, opts.PublishAllMessages().ToRabbitExchange(...)
    /// is publish-only: a published integration event has nothing bound
    /// to receive it and is silently dropped (confirmed live 2026-07-19 -
    /// GetDiscoveryFeed returned empty after CreateDogProfile, and the
    /// RabbitMQ queue list showed no queue at all bound to the exchange).
    /// One queue per module, not per event type - the exchange is a
    /// fanout (see Program.cs), so a single bound queue receives every
    /// published integration event and Wolverine's own message-type
    /// dispatch routes each to whichever local Handle(TEvent) matches,
    /// exactly as if it arrived in-process.
    /// </summary>
    string? IntegrationEventQueueName => null;

    /// <summary>
    /// Hook for any endpoint/middleware wiring beyond what Wolverine.Http's
    /// attribute-routed handlers already register automatically.
    /// </summary>
    void MapEndpoints(WebApplication app)
    {
        // Most modules need nothing here - Wolverine.Http discovers
        // [WolverineGet]/[WolverinePost] handlers via assembly scanning.
    }
}
