using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using K9Crush.Modules.Chat.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.Chat.Api.ReadModels.GetMyConversations;

/// <summary>
/// State-view slice: not one of the 4 slices docs/05-event-modeling-blueprint.md
/// names for this module, but a necessary addition - without it, a
/// caller has no way to discover a conversationId at all once
/// CreateConversationOnMatchHandler creates one asynchronously off a
/// match (there's no synchronous HTTP response carrying it back to
/// whoever just matched). Same class of "necessary minimal technical
/// requirement, not a new business rule" addition as several others in
/// this build-out (e.g. AddDogProfileDetails needing Location).
/// </summary>
public static class GetMyConversationsHandler
{
    [WolverineGet("/api/v1/chat/conversations")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<MyConversationsResponse> Handle(
        ClaimsPrincipal user,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var conversations = await session.Query<ConversationSummary>()
            .Where(x => x.OwnerAId == callerOwnerId || x.OwnerBId == callerOwnerId)
            .ToListAsync(cancellationToken);

        var entries = conversations
            .Select(x => new MyConversationEntry(
                x.Id,
                OtherOwnerId: x.OwnerAId == callerOwnerId ? x.OwnerBId : x.OwnerAId,
                x.CreatedAt))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        return new MyConversationsResponse(entries);
    }
}
