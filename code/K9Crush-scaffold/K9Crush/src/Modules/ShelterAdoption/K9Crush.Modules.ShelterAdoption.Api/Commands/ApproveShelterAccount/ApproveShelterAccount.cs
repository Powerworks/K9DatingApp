namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ApproveShelterAccount;

/// <summary>What this slice hands back to the caller. No request DTO - the
/// route's shelterAccountId is the only input this command needs.</summary>
public sealed record ApproveShelterAccountResponse(Guid ShelterAccountId, string Status);
