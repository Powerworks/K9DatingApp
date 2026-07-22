using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Chat.Api.Commands.SendMessage;
using K9Crush.Modules.Chat.Domain.Events;
using Xunit;

namespace K9Crush.IntegrationTests.Chat;

/// <summary>
/// Layer 3 (TestingApproach.md) - SendMessageHandler needs a real event
/// store (AggregateStreamAsync). Own dedicated container per test class -
/// see CreateConversationOnMatchIntegrationTests' doc comment for why.
/// </summary>
public class SendMessageIntegrationTests : IAsyncLifetime
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
        var result = await SendMessageHandler.Handle(
            Guid.NewGuid(), new SendMessageRequest("Hi!"), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotAParticipant_ReturnsForbid()
    {
        var conversationId = await SeedConversationAsync(Guid.NewGuid(), Guid.NewGuid());

        await using var session = _fixture.Store.LightweightSession();
        var result = await SendMessageHandler.Handle(
            conversationId, new SendMessageRequest("Hi!"), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsAParticipant_AppendsMessageSent()
    {
        var ownerAId = Guid.NewGuid();
        var conversationId = await SeedConversationAsync(ownerAId, Guid.NewGuid());

        await using var session = _fixture.Store.LightweightSession();
        var result = await SendMessageHandler.Handle(
            conversationId, new SendMessageRequest("Hi there!"), BuildUser(ownerAId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<SendMessageResponse>>();
        var messageId = ((Ok<SendMessageResponse>)result.Result).Value!.MessageId;

        await using var verifySession = _fixture.Store.LightweightSession();
        var state = await verifySession.Events.AggregateStreamAsync<LastMessageState>(conversationId);
        state!.MessageId.Should().Be(messageId);
        state.Text.Should().Be("Hi there!");
    }

    /// <summary>
    /// Test-only aggregation state (not production code) - see
    /// CreateConversationOnMatchIntegrationTests' ConversationEventCountState
    /// comment for why AggregateStreamAsync is used here instead of
    /// session.Events.FetchStreamAsync.
    /// </summary>
    internal sealed class LastMessageState
    {
        public Guid MessageId { get; private set; }
        public string Text { get; private set; } = string.Empty;
        public void Apply(MessageSent e)
        {
            MessageId = e.MessageId;
            Text = e.Text;
        }
    }
}
