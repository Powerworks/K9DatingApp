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
    /// </summary>
    [WolverineGet("/api/v1/discovery/feed")]
    public static async Task<DiscoveryFeedResponse> Handle(
        double latitude,
        double longitude,
        double radiusKm,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var candidates = await session.Query<DiscoveryFeedItem>()
            .ToListAsync(cancellationToken);

        var items = candidates
            .Select(c => new DiscoveryFeedEntry(c.Id, c.Breed, DistanceKm(latitude, longitude, c.Latitude, c.Longitude)))
            .Where(x => x.DistanceKm <= radiusKm)
            .OrderBy(x => x.DistanceKm)
            .ToList();

        return new DiscoveryFeedResponse(items);
    }

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
