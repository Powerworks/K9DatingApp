namespace PawMatch.BuildingBlocks.Domain;

/// <summary>
/// Value objects in this codebase are plain C# records - records already give
/// structural equality for free, so no custom base class is needed. This file
/// exists as a documented convention marker: prefer `public record Foo(...)`
/// over a hand-rolled equality implementation whenever the type is a value
/// object (GeoCoordinate, Money, etc.).
/// </summary>
