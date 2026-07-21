using System.Security.Claims;
using FluentAssertions;
using K9Crush.Modules.Chat.Api.ReadModels.GetMyConversations;
using K9Crush.Modules.Chat.Api.ReadModels.Projectors;
using K9Crush.Modules.Chat.Domain.Events;
using Xunit;

namespace K9Crush.IntegrationTests.Chat;

/// <summary>
/// Layer 3 (TestingApproach.md) - GetMyConversationsHandler calls
/// session.Query&lt;ConversationSummary&gt;(). Seeds via the real
/// ConversationCreatedProjectorHandler, same reasoning as
/// GetConversationHistoryIntegrationTests. Own dedicated container per
/// test class - see CreateConversationOnMatchIntegrationTests' doc
/// comment for why.
/// </summary>
public class GetMyConversationsIntegrationTests : IAsyncLifetime
{
    private readonly ChatPostgresFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_ReturnsOnlyTheCallersConversations_WithTheOtherOwnerIdComputedPerCallersPerspective()
    {
        var callerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var unrelatedConversationId = Guid.NewGuid();
        var myConversationId = Guid.NewGuid();

        await using (var seedSession = _fixture.Store.LightweightSession())
        {
            // Caller is OwnerB here - OtherOwnerId in the response should resolve to OwnerA.
            await ConversationCreatedProjectorHandler.Handle(
                new ConversationCreated(myConversationId, otherOwnerId, callerId, myConversationId, DateTimeOffset.UtcNow), seedSession, CancellationToken.None);

            await ConversationCreatedProjectorHandler.Handle(
                new ConversationCreated(unrelatedConversationId, Guid.NewGuid(), Guid.NewGuid(), unrelatedConversationId, DateTimeOffset.UtcNow), seedSession, CancellationToken.None);
        }

        await using var session = _fixture.Store.LightweightSession();
        var response = await GetMyConversationsHandler.Handle(BuildUser(callerId), session, CancellationToken.None);

        response.Conversations.Should().ContainSingle();
        var entry = response.Conversations.Single();
        entry.ConversationId.Should().Be(myConversationId);
        entry.OtherOwnerId.Should().Be(otherOwnerId);
    }
}
