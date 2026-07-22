using System.ComponentModel.DataAnnotations;
using K9Crush.Modules.ShelterAdoption.Domain;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ApplyToFoster;

/// <summary>The request/command for this slice - what the caller sends.</summary>
public sealed record ApplyToFosterRequest(
    HomeType HomeType,
    bool HasGarden,
    bool HasOtherPets,
    DateOnly AvailableFrom);

/// <summary>What this slice hands back to the caller.</summary>
public sealed record ApplyToFosterResponse(Guid FosterApplicationId);
