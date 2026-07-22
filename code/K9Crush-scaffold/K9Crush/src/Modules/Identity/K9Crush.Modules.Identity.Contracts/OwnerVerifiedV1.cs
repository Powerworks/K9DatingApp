using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Identity.Contracts;

/// <summary>
/// Published when Supabase reports an owner's email as confirmed (see
/// Automations/VerifyOwnerOnSupabaseConfirmation). Per HLD Section 1.2,
/// Profiles consumes this to unlock dog-profile creation. Versioned by
/// name suffix, same convention as OwnerRegisteredV1.
/// </summary>
public sealed record OwnerVerifiedV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OwnerId) : IIntegrationEvent;
