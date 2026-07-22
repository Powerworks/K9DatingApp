using System.ComponentModel.DataAnnotations;
using K9Crush.Modules.Places.Domain;

namespace K9Crush.Modules.Places.Api.Commands.CreatePlaceListing;

/// <summary>The request/command for this slice - what the caller sends. See Place.cs's own doc comment for why this command exists at all.</summary>
public sealed record CreatePlaceListingRequest(
    [property: Required, MaxLength(200)] string Name,
    PlaceType PlaceType);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record CreatePlaceListingResponse(Guid PlaceId);
