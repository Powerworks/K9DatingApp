namespace K9Crush.Modules.ShelterAdoption.Api.Commands.ResumeDraftApplication;

/// <summary>What this slice hands back to the caller. Status is either
/// still "Draft" (resumed successfully, safe to edit/submit) or
/// "ClosedDogNoLongerAvailable" (the dog listing was removed - the draft
/// just got closed instead) - 200 either way, the yaml models both as
/// legitimate outcomes of resuming, not one being an error.</summary>
public sealed record ResumeDraftApplicationResponse(Guid ApplicationId, Guid DogListingId, string Status, string Details);
