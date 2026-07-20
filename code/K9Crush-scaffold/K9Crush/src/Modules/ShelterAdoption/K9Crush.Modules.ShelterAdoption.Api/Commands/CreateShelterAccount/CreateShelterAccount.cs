namespace K9Crush.Modules.ShelterAdoption.Api.Commands.CreateShelterAccount;

/// <summary>What this slice hands back to the caller. No request DTO - the
/// route's shelterAccountId is the only input this command needs.</summary>
public sealed record CreateShelterAccountResponse(Guid ShelterAccountId, string Status);
