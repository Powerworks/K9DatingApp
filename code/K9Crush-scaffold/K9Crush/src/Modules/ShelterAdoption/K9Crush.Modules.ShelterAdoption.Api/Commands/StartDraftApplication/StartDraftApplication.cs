namespace K9Crush.Modules.ShelterAdoption.Api.Commands.StartDraftApplication;

/// <summary>What this slice hands back to the caller. WasExisting is true
/// when the applicant already had a draft (or an actively open
/// application) going for this dog and this call just returned it,
/// rather than creating a second one - 200 either way, same idempotent
/// spirit as SubmitApplicationHandler's WasDuplicate.</summary>
public sealed record StartDraftApplicationResponse(Guid ApplicationId, bool WasExisting);
