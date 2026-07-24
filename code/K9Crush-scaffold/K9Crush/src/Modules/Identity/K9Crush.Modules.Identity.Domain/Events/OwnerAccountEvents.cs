namespace K9Crush.Modules.Identity.Domain.Events;

/// <summary>
/// ADR-031 event-sourcing retrofit, Phase 4/5. One record per OwnerAccount
/// transition, matching the entity's own domain methods 1:1. Named
/// OwnerAccountXxxV1 throughout (not the shorter names some methods use,
/// e.g. AccountDeletionRequestedV1) specifically to avoid colliding with
/// this module's own Contracts integration events of similar names
/// (Contracts.AccountDeletionRequestedV1) - domain events stay a
/// completely separate type from the Contracts event representing the
/// same moment, per the retrofit's established convention (see
/// MediaAssetEvents.cs, Phase 1).
/// </summary>
public sealed record OwnerAccountCreatedV1(Guid SupabaseUserId, string Email, DateTimeOffset CreatedAt);

public sealed record OwnerAccountVerifiedV1;

public sealed record OwnerAccountPromotedToShelterV1;

public sealed record OwnerAccountPromotedToAdminV1;

public sealed record OwnerAccountDisplayNameUpdatedV1(string DisplayName);

public sealed record OwnerAccountDeletionRequestedV1(DateTimeOffset RequestedAt);

public sealed record OwnerAccountDeletionConfirmedV1(DateTimeOffset GracePeriodEndsAt);

public sealed record OwnerAccountRecoveredV1;

public sealed record OwnerAccountPermanentlyDeletedV1;
