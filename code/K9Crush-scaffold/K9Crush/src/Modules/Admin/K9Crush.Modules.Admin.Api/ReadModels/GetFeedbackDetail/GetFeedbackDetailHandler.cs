using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Admin.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Admin.Api.ReadModels.GetFeedbackDetail;

/// <summary>
/// State-view slice: the emlang yaml's HandlingGeneralFeedbackSupport
/// chapter's "Feedback Detail" view. Direct document read, same shape as
/// GetDogProfileHandler.
/// </summary>
public static class GetFeedbackDetailHandler
{
    [WolverineGet("/api/v1/admin/feedback/{feedbackId:guid}")]
    [Authorize(Policy = "Admin")]
    public static async Task<Results<Ok<FeedbackDetailResponse>, NotFound>> Handle(
        Guid feedbackId, IQuerySession session, CancellationToken cancellationToken)
    {
        var item = await session.LoadAsync<FeedbackInboxItem>(feedbackId, cancellationToken);
        if (item is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new FeedbackDetailResponse(
            item.Id, item.OwnerId, item.Message, item.SubmittedAt, item.Status.ToString(),
            item.ResponseMessage, item.RespondedAt, item.ResolvedAt));
    }
}
