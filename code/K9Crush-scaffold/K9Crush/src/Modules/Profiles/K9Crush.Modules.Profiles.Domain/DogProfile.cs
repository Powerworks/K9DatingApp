using System.Text.Json.Serialization;
using K9Crush.BuildingBlocks.Domain;

namespace K9Crush.Modules.Profiles.Domain;

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
/// <summary>
/// Draft, still going through the AddDogProfile wizard (not visible
/// anywhere) vs. Published (Discovery-visible - see PublishDogProfileHandler,
/// which is what actually fires DogProfileCreatedV1 now). Only two values
/// exist in the emlang yaml for this chapter; append here, never reorder,
/// same ordinal-serialization reasoning as ShelterAdoption's
/// ApplicationStatus.
/// </summary>
public enum DogProfileStatus
{
    Draft,
    Published
}

public class DogProfile : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    [JsonInclude] public DogProfileStatus Status { get; private set; }
    [JsonInclude] public string Name { get; private set; } = string.Empty;
    [JsonInclude] public string Breed { get; private set; } = string.Empty;
    [JsonInclude] public int AgeInMonths { get; private set; }
    [JsonInclude] public string Bio { get; private set; } = string.Empty;
    [JsonInclude] public GeoCoordinate? Location { get; private set; }
    [JsonInclude] public List<Guid> PhotoIds { get; private set; } = new();
    [JsonInclude] public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // [JsonConstructor] explicitly tells System.Text.Json this non-public
    // constructor is the one to use for deserialization - without it, STJ
    // only considers public constructors and this class has none.
    [JsonConstructor]
    private DogProfile() { }

    /// <summary>
    /// The emlang yaml's AddDogProfile chapter's "Start Dog Profile" ->
    /// "Dog Profile Started" (maxDogProfiles: 10) - a bare Draft shell,
    /// deliberately holding none of the later steps' fields yet.
    /// State-guard (the per-owner 10-profile cap, which needs
    /// session.Query&lt;T&gt;()) lives in StartDogProfileHandler, same
    /// "guard in the handler" pattern as every other slice in this
    /// codebase.
    /// </summary>
    public static DogProfile Start(Guid ownerId) => new()
    {
        OwnerId = ownerId,
        Status = DogProfileStatus.Draft
    };

    /// <summary>
    /// The emlang yaml's "Add Dog Profile Details" -> "Dog Profile Details
    /// Added" (name/breed/age props). Also carries Location - not one of
    /// this chapter's own named props, but Location was already a required
    /// DogProfile field before this chapter existed (DogProfileCreatedV1
    /// needs it for Discovery's proximity search) and this is the closest
    /// existing step to gather it, rather than inventing a separate one
    /// the yaml never names. State-guard (only valid from Draft) lives in
    /// the handler.
    /// </summary>
    public void AddDetails(string name, string breed, int ageInMonths, string bio, GeoCoordinate location)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Dog name is required.", nameof(name));

        if (ageInMonths is < 0 or > 300)
            throw new ArgumentOutOfRangeException(nameof(ageInMonths), "Age must be a realistic value.");

        Name = name.Trim();
        Breed = breed.Trim();
        AgeInMonths = ageInMonths;
        Bio = bio.Trim();
        Location = location;
    }

    /// <summary>The emlang yaml's "Add Dog Profile Photo" -> "Dog Profile
    /// Photo Added". State-guard (only valid from Draft) lives in the
    /// handler.</summary>
    public void AttachPhoto(Guid mediaAssetId)
    {
        if (!PhotoIds.Contains(mediaAssetId))
            PhotoIds.Add(mediaAssetId);
    }

    /// <summary>
    /// The emlang yaml's "Publish Dog Profile" -> "Dog Profile Published".
    /// State-guards (only valid from Draft; the "Reject Publish (No
    /// Photo)" -> "Publish Blocked: Photo Required" branch, since
    /// PhotoIds can't be empty) live in PublishDogProfileHandler, which is
    /// also where DogProfileCreatedV1 now fires (moved from the old
    /// single-shot CreateDogProfileHandler this wizard replaces) - a dog
    /// is only Discovery-visible once actually published, not merely
    /// started.
    /// </summary>
    public void Publish() => Status = DogProfileStatus.Published;

    public void UpdateBio(string bio) => Bio = bio.Trim();
}
