using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Chat.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Chat.Api.ReadModels.GetConversationHistory;

/// <summary>
/// State-view slice: EVENT(s) -> READMODEL -> SCREEN. Per
/// docs/05-event-modeling-blueprint.md's slice table naming
/// ("GetConversationHistory"). Direct document read against
/// ConversationSummary/ChatMessageView - never replays the event stream
/// on this path (that's the projectors' job).
///
/// No pagination - not specified anywhere in the yaml/docs for this
/// increment; add it when message volume in a real conversation actually
/// needs it rather than guessing a page size now.
/// </summary>
public static class GetConversationHistoryHandler
{
    [WolverineGet("/api/v1/chat/conversations/{conversationId:guid}")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<ConversationHistoryResponse>, NotFound, ForbidHttpResult>> Handle(
        Guid conversationId,
        ClaimsPrincipal user,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var conversation = await session.LoadAsync<ConversationSummary>(conversationId, cancellationToken);
        if (conversation is null)
            return TypedResults.NotFound();

        if (conversation.OwnerAId != callerOwnerId && conversation.OwnerBId != callerOwnerId)
            return TypedResults.Forbid();

        var messages = await session.Query<ChatMessageView>()
            .Where(x => x.ConversationId == conversationId)
            .ToListAsync(cancellationToken);

        var entries = messages
            .OrderBy(x => x.SentAt)
            .Select(x => new ChatMessageEntry(x.Id, x.SenderOwnerId, x.Text, x.SentAt))
            .ToList();

        return TypedResults.Ok(new ConversationHistoryResponse(
            conversation.Id, conversation.OwnerAId, conversation.OwnerBId, entries));
    }
}
