using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Chat.Domain.Events;
using Wolverine.Http;

namespace K9Crush.Modules.Chat.Api.Commands.SendMessage;

/// <summary>
/// State-change slice: COMMAND -> EVENT(MessageSent). Per
/// docs/03-solution-architecture.md Section 4 ("write-then-notify") -
/// this increment covers the write half only; SignalR broadcast is a
/// deliberately separate, later follow-up (not built here - see
/// ChatModule.cs's doc comment).
///
/// "Last aggregate stream" pattern for participant validation, same
/// shape as Discovery's UndoLastSwipeHandler/SwipeOnDogHandler - live
/// state via AggregateStreamAsync, never a persisted/shared snapshot
/// (ADR-019).
/// </summary>
public static class SendMessageHandler
{
    [WolverinePost("/api/v1/chat/conversations/{conversationId:guid}/messages")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<SendMessageResponse>, NotFound, ForbidHttpResult>> Handle(
        Guid conversationId,
        SendMessageRequest request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var state = await session.Events.AggregateStreamAsync<SendMessageState>(conversationId, token: cancellationToken);
        if (state is not { Exists: true })
            return TypedResults.NotFound();

        if (!state.HasParticipant(callerOwnerId))
            return TypedResults.Forbid();

        var messageId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        session.Events.Append(conversationId, new MessageSent(conversationId, messageId, callerOwnerId, request.Text, now));
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new SendMessageResponse(messageId, now));
    }
}
