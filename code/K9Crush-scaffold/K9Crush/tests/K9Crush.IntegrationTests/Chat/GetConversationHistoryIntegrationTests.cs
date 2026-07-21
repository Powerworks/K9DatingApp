using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.Chat.Api.ReadModels.GetConversationHistory;
using K9Crush.Modules.Chat.Api.ReadModels.Projectors;
using K9Crush.Modules.Chat.Domain.Events;
using Xunit;

namespace K9Crush.IntegrationTests.Chat;

/// <summary>
/// Layer 3 (TestingApproach.md) - GetConversationHistoryHandler calls
/// session.Query&lt;ChatMessageView&gt;(). Seeds the read model via the
/// real projector handlers (ConversationCreatedProjectorHandler/
/// MessageSentProjectorHandler), same pattern as
/// GetDiscoveryFeedIntegrationTests seeding DiscoveryFeedItem via
/// DogProfileCreatedProjectorHandler - proves the actual projector code,
/// not a shortcut.
///
/// Own dedicated container per test class, not the usual shared
/// ChatPostgresCollection - see CreateConversationOnMatchIntegrationTests'
/// doc comment for why.
/// </summary>
public class GetConversationHistoryIntegrationTests : IAsyncLifetime
{
    private readonly ChatPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenConversationDoesNotExist_ReturnsNotFound()
    {
        await using var session = _fixture.Store.LightweightSession();
        var result = await GetConversationHistoryHandler.Handle(Guid.NewGuid(), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotAParticipant_ReturnsForbid()
    {
        var conversationId = Guid.NewGuid();
        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            await ConversationCreatedProjectorHandler.Handle(
                new ConversationCreated(conversationId, Guid.NewGuid(), Guid.NewGuid(), conversationId, DateTimeOffset.UtcNow),
                seedSession, CancellationToken.None);
        }

        await using var session = _fixture.Store.LightweightSession();
        var result = await GetConversationHistoryHandler.Handle(conversationId, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_ReturnsMessagesInChronologicalOrder()
    {
        var conversationId = Guid.NewGuid();
        var ownerAId = Guid.NewGuid();
        var ownerBId = Guid.NewGuid();

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            await ConversationCreatedProjectorHandler.Handle(
                new ConversationCreated(conversationId, ownerAId, ownerBId, conversationId, DateTimeOffset.UtcNow), seedSession, CancellationToken.None);

            var now = DateTimeOffset.UtcNow;
            await MessageSentProjectorHandler.Handle(
                new MessageSent(conversationId, Guid.NewGuid(), ownerBId, "Second", now.AddSeconds(1)), seedSession, CancellationToken.None);
            await MessageSentProjectorHandler.Handle(
                new MessageSent(conversationId, Guid.NewGuid(), ownerAId, "First", now), seedSession, CancellationToken.None);
        }

        await using var session = _fixture.Store.LightweightSession();
        var result = await GetConversationHistoryHandler.Handle(conversationId, BuildUser(ownerAId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ConversationHistoryResponse>>();
        var response = ((Ok<ConversationHistoryResponse>)result.Result).Value!;
        response.Messages.Should().HaveCount(2);
        response.Messages.Select(m => m.Text).Should().ContainInOrder("First", "Second");
    }
}
