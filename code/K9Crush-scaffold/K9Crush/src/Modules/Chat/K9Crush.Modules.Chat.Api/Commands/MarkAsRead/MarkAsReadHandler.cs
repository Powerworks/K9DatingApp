using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Chat.Domain.Events;
using Wolverine.Http;

namespace K9Crush.Modules.Chat.Api.Commands.MarkAsRead;

/// <summary>
/// State-change slice: COMMAND -> EVENT(MessageRead). Same participant-
/// validation shape as SendMessageHandler.
/// </summary>
public static class MarkAsReadHandler
{
    [WolverinePost("/api/v1/chat/conversations/{conversationId:guid}/read")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<MarkAsReadResponse>, NotFound, ForbidHttpResult>> Handle(
        Guid conversationId,
        MarkAsReadRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var state = await session.Events.AggregateStreamAsync<MarkAsReadState>(conversationId, token: cancellationToken);
        if (state is not { Exists: true })
            return TypedResults.NotFound();

        if (!state.HasParticipant(callerOwnerId))
            return TypedResults.Forbid();

        session.Events.Append(conversationId, new MessageRead(conversationId, callerOwnerId, request.LastReadMessageId, DateTimeOffset.UtcNow));
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new MarkAsReadResponse(conversationId, request.LastReadMessageId));
    }
}
