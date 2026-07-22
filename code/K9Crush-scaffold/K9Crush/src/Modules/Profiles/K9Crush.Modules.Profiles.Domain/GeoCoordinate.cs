namespace K9Crush.Modules.Profiles.Domain;

/// <summary>
/// Value object (see BuildingBlocks.Domain.ValueObject convention note) -
/// a plain record gives structural equality for free.
/// </summary>
public record GeoCoordinate(double Latitude, double Longitude)
{
    public static GeoCoordinate Create(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude));

        return new GeoCoordinate(latitude, longitude);
    }
}
