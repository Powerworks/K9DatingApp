namespace K9Crush.Modules.Discovery.Api.ReadModels.GetDiscoveryFeed;

/// <summary>
/// Covers TheWindowShopper chapter's "Preview Nearby Dogs" -> "Nearby Dogs
/// Previewed" -> "Nearby Dogs Preview" view as one state-view slice, same
/// consolidation pattern used throughout this build-out (see
/// GetApplicationStatusHandler for the precedent). Anonymous - Guests
/// browse this exact same feed before signing up.
///
/// MatchType is always "dog_to_dog" right now - the yaml's other named
/// value, "shelter_dog", would need ShelterAdoption to publish an
/// integration event when a DogListing is added (it doesn't yet;
/// AddDogListingHandler is purely internal to that module) and this
/// module to consume it into DiscoveryFeedItem with a matchType
/// discriminator. Flagged as a real gap, not silently ignored - see
/// GetDiscoveryFeedHandler.
/// </summary>
public sealed record DiscoveryFeedEntry(Guid DogProfileId, string Name, string Breed, double DistanceMiles, string MatchType);

public sealed record DiscoveryFeedResponse(IReadOnlyList<DiscoveryFeedEntry> Items);
