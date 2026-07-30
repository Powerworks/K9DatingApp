using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.ShelterAdoption.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetSurrenderReviewQueue;

/// <summary>
/// State-view slice: EVENT(s) -> READMODEL -> SCREEN. Direct document
/// query over DogSurrenderRequest, covers Spec/K9CRUSH.emlang.v3.yaml's
/// SurrenderingYourDog chapter's "Surrender Review Queue" view - every
/// surrender request platform-wide, not scoped to a single shelter (no
/// shelter is chosen until AcceptDogSurrenderHandler), same "Admin sees
/// everything" reasoning as GetFeedbackInboxHandler/GetModerationQueueHandler.
/// Unfiltered by status (unlike GetPendingApplicationsQueueHandler's
/// IsOpen-only filter) - the yaml's own "Surrender Review Queue" sample
/// shows a status field per item rather than framing this as an
/// attention-needed-only list.
/// </summary>
public static class GetSurrenderReviewQueueHandler
{
    [WolverineGet("/api/v1/shelter-adoption/surrender-requests")]
    [Authorize(Policy = "Admin")]
    public static async Task<SurrenderReviewQueueResponse> Handle(
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var requests = await session.Query<DogSurrenderRequest>().ToListAsync(cancellationToken);

        var items = requests
            .Select(x => new SurrenderRequestSummary(x.Id, x.DogName, x.RequestedByOwnerId, x.Status.ToString()))
            .ToList();

        return new SurrenderReviewQueueResponse(items);
    }
}
