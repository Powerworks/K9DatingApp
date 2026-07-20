using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Identity.Contracts;

/// <summary>
/// Published when a new OwnerAccount is provisioned from a Supabase
/// auth.users insert (see Automations/ProvisionOwnerOnSupabaseSignup).
/// Consumed by Profiles per HLD Section 1.2 ("Consumes: OwnerVerified
/// (unlocks profile creation)") - that's a separate, later event/slice;
/// this one is the registration half only. Versioned by name suffix -
/// if a breaking change is ever needed, add OwnerRegisteredV2 rather
/// than editing this one.
/// </summary>
public sealed record OwnerRegisteredV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OwnerId,
    string Email) : IIntegrationEvent;
