namespace K9Crush.Modules.Discovery.Api.ReadModels.GetDiscoveryFeed;

public sealed record DiscoveryFeedEntry(Guid DogProfileId, string Breed, double DistanceKm);

public sealed record DiscoveryFeedResponse(IReadOnlyList<DiscoveryFeedEntry> Items);
