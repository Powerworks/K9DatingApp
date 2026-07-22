using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetDraftApplications;

/// <summary>
/// State-view slice: EVENT(s) -> READMODEL -> SCREEN. Covers the emlang
/// yaml's "View Draft Applications" command+event pair and the "Draft
/// Applications" view as one slice, same consolidation applied
/// throughout this build-out. Own-drafts-only (VerifiedOwner, filtered
/// to the caller's ApplicantOwnerId) - no separate ownership check needed
/// since the query itself is already scoped to the caller.
///
/// DogName requires loading each draft's DogListing (Application doesn't
/// store the dog's name itself) - N+1 loads, but bounded by
/// StartDraftApplicationHandler's 3-draft cap, so never worth a fancier
/// join for this volume.
/// </summary>
public static class GetDraftApplicationsHandler
{
    [WolverineGet("/api/v1/shelter-adoption/draft-applications")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<DraftApplicationsResponse> Handle(
        ClaimsPrincipal user,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var applicantOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var drafts = await session.Query<Application>()
            .Where(x => x.ApplicantOwnerId == applicantOwnerId && x.Status == ApplicationStatus.Draft)
            .ToListAsync(cancellationToken);

        var items = new List<DraftApplicationSummary>();
        foreach (var draft in drafts)
        {
            var dogListing = await session.LoadAsync<DogListing>(draft.DogListingId, cancellationToken);
            items.Add(new DraftApplicationSummary(draft.Id, draft.DogListingId, dogListing?.Name ?? "(listing removed)", draft.LastEditedAt));
        }

        return new DraftApplicationsResponse(items);
    }
}
