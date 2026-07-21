namespace K9Crush.Modules.Notifications.Domain;

/// <summary>
/// Current-state Marten document, kept up to date by a projector reacting
/// to Identity's OwnerRegisteredV1 (see Api/Automations/IndexOwnerContact).
/// Exists because NotifyOnMatchHandler needs an actual email address to
/// send to, and this module can only see other modules' Contracts, never
/// their Domain - same "own local denormalized copy" pattern
/// DiscoveryFeedItem already uses for DogProfileCreatedV1.
///
/// Plain class, not Entity-derived (Id is the owner's own id, not a
/// freshly generated one) - same shape as DiscoveryFeedItem.
/// </summary>
public class OwnerContact
{
    public Guid Id { get; set; } // same as OwnerId
    public string Email { get; set; } = default!;
}
