using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Chat.Api.Commands.MarkAsRead;
using K9Crush.Modules.Chat.Domain.Events;
using Xunit;

namespace K9Crush.IntegrationTests.Chat;

/// <summary>
/// Layer 3 (TestingApproach.md) - MarkAsReadHandler needs a real event
/// store (AggregateStreamAsync). Own dedicated container per test class -
/// see CreateConversationOnMatchIntegrationTests' doc comment for why.
/// </summary>
public class MarkAsReadIntegrationTests : IAsyncLifetime
{
    private readonly ChatPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private async Task<Guid> SeedConversationAsync(Guid ownerAId, Guid ownerBId)
    {
        var conversationId = Guid.NewGuid();
        await using var session = _fixture.Store.LightweightSession();
        session.Events.Append(conversationId, new ConversationCreated(conversationId, ownerAId, ownerBId, conversationId, DateTimeOffset.UtcNow));
        await session.SaveChangesAsync();
        return conversationId;
    }

    [Fact]
    public async Task Handle_WhenConversationDoesNotExist_ReturnsNotFound()
    {
        await using var session = _fixture.Store.LightweightSession();
        var result = await MarkAsReadHandler.Handle(
            Guid.NewGuid(), new MarkAsReadRequest(Guid.NewGuid()), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotAParticipant_ReturnsForbid()
    {
        var conversationId = await SeedConversationAsync(Guid.NewGuid(), Guid.NewGuid());

        await using var session = _fixture.Store.LightweightSession();
        var result = await MarkAsReadHandler.Handle(
            conversationId, new MarkAsReadRequest(Guid.NewGuid()), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsAParticipant_AppendsMessageRead()
    {
        var ownerBId = Guid.NewGuid();
        var conversationId = await SeedConversationAsync(Guid.NewGuid(), ownerBId);
        var lastReadMessageId = Guid.NewGuid();

        await using var session = _fixture.Store.LightweightSession();
        var result = await MarkAsReadHandler.Handle(
            conversationId, new MarkAsReadRequest(lastReadMessageId), BuildUser(ownerBId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<MarkAsReadResponse>>();

        await using var verifySession = _fixture.Store.LightweightSession();
        var state = await verifySession.Events.AggregateStreamAsync<LastReadState>(conversationId);
        state!.ReaderOwnerId.Should().Be(ownerBId);
        state.LastReadMessageId.Should().Be(lastReadMessageId);
    }

    /// <summary>
    /// Test-only aggregation state (not production code) - see
    /// CreateConversationOnMatchIntegrationTests' ConversationEventCountState
    /// comment for why AggregateStreamAsync is used here instead of
    /// session.Events.FetchStreamAsync.
    /// </summary>
    internal sealed class LastReadState
    {
        public Guid ReaderOwnerId { get; private set; }
        public Guid LastReadMessageId { get; private set; }
        public void Apply(MessageRead e)
        {
            ReaderOwnerId = e.ReaderOwnerId;
            LastReadMessageId = e.LastReadMessageId;
        }
    }
}
