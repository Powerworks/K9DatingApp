using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.Commands.StartDraftApplication;

/// <summary>
/// State-change slice: the emlang yaml's ResumingADraftApplication
/// starting point / TheWouldBeAdopter's "Gate Application Start" (which
/// carries a draftApplicationId prop). Creates a Draft-status
/// Application, editable and resumable before real submission - see
/// Application.StartDraft()'s comment for why this coexists with
/// SubmitApplicationHandler's express path rather than replacing it.
///
/// maxDraftApplications (3, per the yaml) is a separate counter from
/// SubmitApplicationHandler's maxOpenApplications - drafts don't occupy
/// a real application slot with the shelter yet, see
/// Application.IsOpen's comment.
///
/// Any verified owner can start a draft - no Shelter/Admin role needed.
/// </summary>
public static class StartDraftApplicationHandler
{
    private const int MaxDraftApplications = 3;

    [WolverinePost("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}/draft-applications")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<StartDraftApplicationResponse>, NotFound, Conflict<string>>> Handle(
        Guid dogListingId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var applicantOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var dogListing = await session.LoadAsync<DogListing>(dogListingId, cancellationToken);
        if (dogListing is null || dogListing.IsRemoved)
            return TypedResults.NotFound();

        var applicantApplications = await session.Query<Application>()
            .Where(x => x.ApplicantOwnerId == applicantOwnerId)
            .ToListAsync(cancellationToken);

        var existingForThisDog = applicantApplications.FirstOrDefault(
            x => x.DogListingId == dogListingId && (x.Status == ApplicationStatus.Draft || x.IsOpen));
        if (existingForThisDog is not null)
            return TypedResults.Ok(new StartDraftApplicationResponse(existingForThisDog.Id, WasExisting: true));

        var draftCount = applicantApplications.Count(x => x.Status == ApplicationStatus.Draft);
        if (draftCount >= MaxDraftApplications)
            return TypedResults.Conflict($"Draft application limit reached - at most {MaxDraftApplications} drafts allowed.");

        var (application, @event) = Application.StartDraftNew(applicantOwnerId, dogListingId, dogListing.ShelterAccountId);
        session.Events.StartStream<Application>(application.Id, @event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new StartDraftApplicationResponse(application.Id, WasExisting: false));
    }
}
