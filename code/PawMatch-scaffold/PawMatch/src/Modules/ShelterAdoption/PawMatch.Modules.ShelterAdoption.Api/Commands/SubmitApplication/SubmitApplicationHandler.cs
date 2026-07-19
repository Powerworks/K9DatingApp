using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using PawMatch.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace PawMatch.Modules.ShelterAdoption.Api.Commands.SubmitApplication;

/// <summary>
/// State-change slice: the emlang yaml's "Submit Application" ->
/// "Application Submitted", consolidating three related outcomes from
/// TheWouldBeAdopter into one handler rather than three separate slices,
/// since they're all the same decision point ("can this submission
/// proceed") with different answers:
/// - Normal path: no open application yet, under the limit -> create,
///   Application Submitted.
/// - "Resubmit Same Application" -> "Duplicate Submission Ignored": an
///   open application for this exact dog already exists - returns the
///   existing one, WasDuplicate: true, not an error.
/// - "Reject Application Submission" -> "Application Limit Reached": no
///   duplicate, but the applicant already has maxOpenApplications (3, per
///   the yaml) open applications across all dogs - 409.
///
/// No caller/member content beyond the dog reference - the yaml doesn't
/// specify application form fields (cover letter, home situation, etc.)
/// beyond the counters above; add a request body if a real form
/// requirement shows up.
///
/// Updated (drafts feature): also covers the emlang yaml's "Submit
/// Application" -> "Application Submitted" when it carries a
/// draftApplicationId - if the applicant already has a Draft going for
/// this exact dog (started via StartDraftApplicationHandler, possibly
/// edited via EditApplicationDetailsHandler), this graduates it straight
/// to Pending instead of creating a second Application or treating it as
/// a duplicate. A Draft doesn't count toward maxOpenApplications (see
/// Application.IsOpen), so graduating one is never blocked by the limit
/// that would apply to a brand-new submission.
///
/// Any verified owner can apply - no Shelter/Admin role needed.
/// </summary>
public static class SubmitApplicationHandler
{
    private const int MaxOpenApplications = 3;

    [WolverinePost("/api/v1/shelter-adoption/dog-listings/{dogListingId:guid}/applications")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<SubmitApplicationResponse>, NotFound, Conflict<string>>> Handle(
        Guid dogListingId,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var applicantOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var dogListing = await session.LoadAsync<DogListing>(dogListingId, cancellationToken);
        if (dogListing is null)
            return TypedResults.NotFound();

        var applicantApplications = await session.Query<Application>()
            .Where(x => x.ApplicantOwnerId == applicantOwnerId)
            .ToListAsync(cancellationToken);

        var existingForThisDog = applicantApplications.FirstOrDefault(x => x.DogListingId == dogListingId && x.IsOpen);
        if (existingForThisDog is not null)
            return TypedResults.Ok(new SubmitApplicationResponse(existingForThisDog.Id, WasDuplicate: true));

        var draftForThisDog = applicantApplications.FirstOrDefault(
            x => x.DogListingId == dogListingId && x.Status == ApplicationStatus.Draft);
        if (draftForThisDog is not null)
        {
            draftForThisDog.SubmitDraft();
            session.Store(draftForThisDog);
            await session.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(new SubmitApplicationResponse(draftForThisDog.Id, WasDuplicate: false));
        }

        var openCount = applicantApplications.Count(x => x.IsOpen);
        if (openCount >= MaxOpenApplications)
            return TypedResults.Conflict($"Application limit reached - at most {MaxOpenApplications} open applications allowed.");

        var application = Application.Submit(applicantOwnerId, dogListingId, dogListing.ShelterAccountId);
        session.Store(application);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new SubmitApplicationResponse(application.Id, WasDuplicate: false));
    }
}
