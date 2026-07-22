using System.Security.Cryptography;
using System.Text;

namespace K9Crush.Modules.Discovery.Domain;

/// <summary>
/// Deterministic stream identity for the swipe relationship between one
/// unordered pair of dogs. Deliberately just an ID helper - NOT a state
/// bundle. Per ADR-019, this module used to also have a MatchAggregate
/// type that combined this identity logic with a persisted, shared
/// command-state snapshot (DogAId/DogBId/DogALiked/DogBLiked/IsMatched);
/// that bundle has been removed. Command state now lives per-command in
/// e.g. Automations/DetectMutualMatch/DetectMutualMatchState.cs, computed
/// live from the stream, never persisted or shared.
/// </summary>
public static class MatchStream
{
    /// <summary>
    /// Sorting the two guids before hashing guarantees Like(A,B) and
    /// Like(B,A) resolve to the identical stream, so "has A already liked
    /// B" and "has B already liked A" never need a separate reverse-lookup
    /// read model on the hot path.
    /// </summary>
    public static Guid IdFor(Guid dogId1, Guid dogId2)
    {
        var (first, second) = dogId1.CompareTo(dogId2) <= 0 ? (dogId1, dogId2) : (dogId2, dogId1);
        var bytes = Encoding.UTF8.GetBytes($"{first:N}:{second:N}");
        var hash = MD5.HashData(bytes); // deterministic, not security-sensitive - just a stream key
        return new Guid(hash);
    }
}
