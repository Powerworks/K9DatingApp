using System.Text.Json.Serialization;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PawMatch.Modules.Identity.Contracts;
using PawMatch.Modules.Identity.Domain;
using Wolverine.Http;

namespace PawMatch.Modules.Identity.Api.Automations.ProvisionOwnerOnSupabaseSignup;

/// <summary>
/// Supabase Database Webhooks serialize the raw Postgres row - snake_case
/// column names, not this codebase's usual PascalCase request records.
/// Shape is Supabase's documented Database Webhooks format; unverified
/// against a live Supabase project this session, same caveat as every
/// other Supabase integration point flagged in GETTING_STARTED.md.
/// </summary>
public sealed record SupabaseUserWebhookPayload(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("table")] string? Table,
    [property: JsonPropertyName("schema")] string? Schema,
    [property: JsonPropertyName("record")] SupabaseUserRecord? Record);

public sealed record SupabaseUserRecord(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

/// <summary>
/// Automation slice: EVENT(Supabase auth.users INSERT) -> AUTOMATION ->
/// EVENT(OwnerRegisteredV1). Bridges Supabase's own user lifecycle into
/// our OwnerAccount document. Per ADR-005, Identity never implements
/// signup/login/password-reset/confirmation-email itself - the client
/// talks to Supabase's own /auth/v1 endpoints directly for all of that;
/// this slice only reacts once Supabase has actually created the user.
///
/// DEVIATION FROM THE USUAL "AUTOMATIONS HAVE NO HTTP ROUTE" RULE
/// (docs/05-event-modeling-blueprint.md Section 6): every other
/// automation in this codebase (e.g. Discovery's DetectMutualMatch) is
/// triggered by an in-process Marten-forwarded domain event. This one's
/// trigger is external - Supabase has no way to publish into our
/// Wolverine bus directly, only to POST a Database Webhook over HTTP. An
/// HTTP-triggered automation is still an automation (it makes a decision
/// and produces an event; nothing calls it the way a user deliberately
/// calls a command), it just necessarily has a route. If a future
/// architecture-fitness test enforces "no HTTP route under
/// Automations/**" literally, this slice needs an explicit carve-out,
/// not a redesign.
///
/// Idempotent by design: Database Webhooks can redeliver, so
/// re-provisioning an OwnerAccount that already exists is a no-op, not
/// an error.
///
/// Auth: Supabase does not cryptographically sign Database Webhook
/// payloads. Mitigation is a shared-secret header ("X-Webhook-Secret"),
/// set as a custom header when configuring the webhook in the Supabase
/// dashboard and matched against Supabase:WebhookSecret here - this
/// header name is this codebase's own convention, not something Supabase
/// requires. Confirm against however the webhook actually gets
/// configured once there's a live project to test against.
/// </summary>
public static class ProvisionOwnerOnSupabaseSignupHandler
{
    [WolverinePost("/api/v1/identity/webhooks/supabase/user-created")]
    public static async Task<(IResult, OwnerRegisteredV1?)> Handle(
        SupabaseUserWebhookPayload payload,
        HttpRequest httpRequest,
        IConfiguration configuration,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var expectedSecret = configuration["Supabase:WebhookSecret"];
        var providedSecret = httpRequest.Headers["X-Webhook-Secret"].ToString();

        if (string.IsNullOrEmpty(expectedSecret) || providedSecret != expectedSecret)
            return (Results.Unauthorized(), null);

        if (payload is not { Type: "INSERT", Schema: "auth", Table: "users", Record: not null })
            return (Results.Ok(), null); // Not a user-created row - ack anyway so Supabase doesn't retry.

        var existing = await session.LoadAsync<OwnerAccount>(payload.Record.Id, cancellationToken);
        if (existing is not null)
            return (Results.Ok(), null); // Already provisioned - redelivery, not an error.

        var ownerAccount = OwnerAccount.Create(payload.Record.Id, payload.Record.Email, payload.Record.CreatedAt);
        session.Store(ownerAccount);
        await session.SaveChangesAsync(cancellationToken);

        var integrationEvent = new OwnerRegisteredV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            OwnerId: ownerAccount.Id,
            Email: ownerAccount.Email);

        return (Results.Ok(), integrationEvent);
    }
}
