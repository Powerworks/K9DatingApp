using System.Text.Json.Serialization;
using PawMatch.BuildingBlocks.Domain;

namespace PawMatch.Modules.Profiles.Domain;

/// <summary>
/// Current-state Marten document (not event-sourced - see ADR/Solution
/// Architecture doc section 4 for why Profiles is document-centric while
/// Discovery/Chat are event-sourced). Marten persists this directly via
/// session.Store(dogProfile); there's no event stream to replay.
///
/// SERIALIZATION PATTERN - read this before adding another document type:
/// this class deliberately keeps its constructor and property setters
/// non-public so invariants can only be enforced through Create() and the
/// domain methods below, never by external code setting a property
/// directly. That's a good DDD instinct, but it directly conflicts with
/// Marten's default serializer (System.Text.Json's reflection-based
/// converter), which by default only uses PUBLIC constructors and only
/// populates PUBLIC settable members - it silently can't materialize this
/// type otherwise, and originally didn't (a GET request 500'd trying to
/// deserialize a document that was, in fact, saved correctly - the write
/// path was always fine, only the read-back was broken).
///
/// The fix, applied here and required on every future document-style
/// entity (Identity, Subscriptions, Moderation, Places, Shelter &
/// Adoption, etc. - anything deriving from Entity, per the CRUD/document
/// classification in the Solution Architecture doc): mark the
/// constructor Marten should use with [JsonConstructor], and mark every
/// non-publicly-settable property with [JsonInclude]. This keeps the
/// properties genuinely non-public to every OTHER caller - only the
/// serializer gets the exception, via these specific attributes, not a
/// blanket "make everything public" concession.
/// </summary>
public class DogProfile : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    [JsonInclude] public string Name { get; private set; } = default!;
    [JsonInclude] public string Breed { get; private set; } = default!;
    [JsonInclude] public int AgeInMonths { get; private set; }
    [JsonInclude] public string Bio { get; private set; } = string.Empty;
    [JsonInclude] public GeoCoordinate Location { get; private set; } = default!;
    [JsonInclude] public List<Guid> PhotoIds { get; private set; } = new();
    [JsonInclude] public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // [JsonConstructor] explicitly tells System.Text.Json this non-public
    // constructor is the one to use for deserialization - without it, STJ
    // only considers public constructors and this class has none.
    [JsonConstructor]
    private DogProfile() { }

    public static DogProfile Create(
        Guid ownerId,
        string name,
        string breed,
        int ageInMonths,
        string bio,
        GeoCoordinate location)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Dog name is required.", nameof(name));

        if (ageInMonths is < 0 or > 300)
            throw new ArgumentOutOfRangeException(nameof(ageInMonths), "Age must be a realistic value.");

        return new DogProfile
        {
            OwnerId = ownerId,
            Name = name.Trim(),
            Breed = breed.Trim(),
            AgeInMonths = ageInMonths,
            Bio = bio.Trim(),
            Location = location
        };
    }

    public void AttachPhoto(Guid mediaAssetId)
    {
        if (!PhotoIds.Contains(mediaAssetId))
            PhotoIds.Add(mediaAssetId);
    }

    public void UpdateBio(string bio) => Bio = bio.Trim();
}
