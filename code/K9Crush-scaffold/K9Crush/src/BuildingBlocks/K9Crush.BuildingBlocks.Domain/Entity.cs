using System.Text.Json.Serialization;

namespace K9Crush.BuildingBlocks.Domain;

/// <summary>
/// Base type for document-centric entities (Identity, ShelterAdoption, Media,
/// Notifications, Admin). These are current-state documents stored directly
/// via Marten's document store - no event stream.
/// </summary>
public abstract class Entity
{
    // [JsonInclude] is required because the setter is non-public - Marten's
    // default System.Text.Json-based serializer only populates public
    // settable members unless told otherwise. See
    // docs/05-event-modeling-blueprint.md Section 6.1 for the fuller
    // writeup of this pattern (private ctor + private setters +
    // [JsonConstructor]/[JsonInclude]) that every document-style entity
    // derived from this class needs to follow - this bit everyone the
    // first time a GET actually tried to deserialize a stored document.
    [JsonInclude]
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
