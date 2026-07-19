using System.Text.Json.Serialization;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PawMatch.Modules.Identity.Contracts;
using PawMatch.Modules.Identity.Domain;
using Wolverine.Http;

namespace PawMatch.Modules.Identity.Api.Automations.VerifyOwnerOnSupabaseConfirmation;

/// <summary>
/// Own payload DTOs, scoped to exactly the two fields this automation
/// needs - deliberately not shared with
/// Automations/ProvisionOwnerOnSupabaseSignup's payload types even
/// though both parse a Supabase auth.users row. This is a different
/// Database Webhook subscription (UPDATE, not INSERT) with a different
/// shape need (old_record, to detect a transition) - small duplication
/// here is cheaper than a shared type two independent automations both
/// reach into.
/// </summary>
public sealed record SupabaseUserUpdatePayload(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("table")] string? Table,
    [property: JsonPropertyName("schema")] string? Schema,
    [property: JsonPropertyName("record")] SupabaseUserUpdateRecord? Record,
    [property: JsonPropertyName("old_record")] SupabaseUserUpdateRecord? OldRecord);

public sealed record SupabaseUserUpdateRecord(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("email_confirmed_at")] DateTimeOffset? EmailConfirmedAt);

/// <summary>
/// Automation slice: EVENT(Supabase auth.users UPDATE, email just
/// confirmed) -> AUTOMATION -> EVENT(OwnerVerifiedV1). Sibling to
/// Automations/ProvisionOwnerOnSupabaseSignup - same rationale for why an
/// automation has an HTTP route here (Supabase can only reach us via
/// webhook POST, never directly into our Wolverine bus) - see that
/// file's comment for the full writeup, not repeated here. Same
/// shared-secret header auth as that slice, same
/// Supabase:WebhookSecret config value.
///
/// Separate endpoint from ProvisionOwnerOnSupabaseSignup on purpose: this
/// reacts to a Database Webhook configured on auth.users UPDATE, not
/// INSERT - a distinct Supabase webhook subscription, so a distinct
/// route rather than one endpoint branching on `type`.
///
/// auth.users UPDATE fires for lots of unrelated changes (last_sign_in_at
/// on every login, etc.) - this only acts on the specific
/// null-to-non-null transition of email_confirmed_at, using old_record
/// to distinguish "just confirmed" from "already confirmed, some other
/// column changed" or "still unconfirmed." Idempotent either way: if
/// OwnerAccount doesn't exist yet (out-of-order delivery relative to the
/// INSERT webhook) or is already verified, this is a no-op, not an error.
/// </summary>
public static class VerifyOwnerOnSupabaseConfirmationHandler
{
    [WolverinePost("/api/v1/identity/webhooks/supabase/user-confirmed")]
    public static async Task<(IResult, OwnerVerifiedV1?)> Handle(
        SupabaseUserUpdatePayload payload,
        HttpRequest httpRequest,
        IConfiguration configuration,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var expectedSecret = configuration["Supabase:WebhookSecret"];
        var providedSecret = httpRequest.Headers["X-Webhook-Secret"].ToString();

        if (string.IsNullOrEmpty(expectedSecret) || providedSecret != expectedSecret)
            return (Results.Unauthorized(), null);

        var justConfirmed = payload is
        {
            Type: "UPDATE",
            Schema: "auth",
            Table: "users",
            Record.EmailConfirmedAt: not null
        } && payload.OldRecord?.EmailConfirmedAt is null;

        if (!justConfirmed)
            return (Results.Ok(), null); // Not the transition this slice cares about - ack anyway so Supabase doesn't retry.

        var ownerAccount = await session.LoadAsync<OwnerAccount>(payload.Record!.Id, cancellationToken);
        if (ownerAccount is null || ownerAccount.IsVerified)
            return (Results.Ok(), null); // Not provisioned yet, or already verified - no-op either way.

        ownerAccount.MarkVerified();
        session.Store(ownerAccount);
        await session.SaveChangesAsync(cancellationToken);

        var integrationEvent = new OwnerVerifiedV1(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            OwnerId: ownerAccount.Id);

        return (Results.Ok(), integrationEvent);
    }
}
