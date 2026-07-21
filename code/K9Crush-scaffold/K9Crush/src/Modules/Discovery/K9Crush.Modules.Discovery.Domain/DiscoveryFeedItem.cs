namespace K9Crush.Modules.Discovery.Domain;

/// <summary>
/// Read-model document, kept up to date by a Marten async daemon
/// projection off DogProfileCreated (consumed from the Profiles module)
/// and swipe events. Exists so GetDiscoveryFeed never has to replay event
/// streams on the read path - it just queries this document like any
/// other Marten document.
/// </summary>
public class DiscoveryFeedItem
{
    public Guid Id { get; set; } // same as DogProfileId
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = default!;
    public string Breed { get; set; } = default!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTimeOffset IndexedAt { get; set; }
}
