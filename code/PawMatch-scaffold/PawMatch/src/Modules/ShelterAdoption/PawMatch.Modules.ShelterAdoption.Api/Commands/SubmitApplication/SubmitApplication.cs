namespace PawMatch.Modules.ShelterAdoption.Api.Commands.SubmitApplication;

/// <summary>What this slice hands back to the caller. WasDuplicate is
/// true when an open application for this dog already existed and this
/// call was a no-op (the emlang yaml's "Duplicate Submission Ignored"),
/// not an error - 200 either way.</summary>
public sealed record SubmitApplicationResponse(Guid ApplicationId, bool WasDuplicate);
