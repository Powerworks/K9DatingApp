using Marten;
using K9Crush.Modules.Discovery.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Discovery.Api.ReadModels.GetDiscoveryFeed;

public static class GetDiscoveryFeedHandler
{
    /// <summary>
    /// MVP version: bounding-box filter in-memory over the DiscoveryFeedItem
    /// read model. Swap for a PostGIS ST_DWithin query (see Solution
    /// Architecture doc, Section 4) once volume justifies it - the read
    /// model shape doesn't need to change, only this query.
    ///
    /// Distances are in miles throughout (query radius and response) -
    /// matches the emlang yaml's "Nearby Dogs Preview" view prop
    /// (distanceMiles), previously mismatched against this endpoint's old
    /// km-based DistanceKm field.
    /// </summary>
    [WolverineGet("/api/v1/discovery/feed")]
    public static async Task<DiscoveryFeedResponse> Handle(
        double latitude,
        double longitude,
        double radiusMiles,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var candidates = await session.Query<DiscoveryFeedItem>()
            .ToListAsync(cancellationToken);

        var items = candidates
            .Select(c => new DiscoveryFeedEntry(
                c.Id,
                c.Name,
                c.Breed,
                DistanceMiles(latitude, longitude, c.Latitude, c.Longitude),
                MatchType: "dog_to_dog"))
            .Where(x => x.DistanceMiles <= radiusMiles)
            .OrderBy(x => x.DistanceMiles)
            .ToList();

        return new DiscoveryFeedResponse(items);
    }

    private static double DistanceMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMiles = 3958.8;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMiles * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
