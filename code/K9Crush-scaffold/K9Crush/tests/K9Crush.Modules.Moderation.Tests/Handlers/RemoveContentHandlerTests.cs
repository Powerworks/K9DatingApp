using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Moderation.Api.Commands.RemoveContent;
using K9Crush.Modules.Moderation.Domain;
using Xunit;

namespace K9Crush.Modules.Moderation.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RemoveContentHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class RemoveContentHandlerTests
{
    [Fact]
    public async Task Handle_WhenFlagDoesNotExist_ReturnsNotFoundAndCascadesNothing()
    {
        var flagId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FlaggedContent>(flagId, Arg.Any<CancellationToken>()).Returns((FlaggedContent?)null);

        var (result, integrationEvent) = await RemoveContentHandler.Handle(flagId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenFlagExists_MarksContentRemovedAndCascadesContentRemovalRequested()
    {
        var contentId = Guid.NewGuid();
        var flag = FlaggedContent.Create(ContentType.Media, contentId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FlaggedContent>(flag.Id, Arg.Any<CancellationToken>()).Returns(flag);

        var (result, integrationEvent) = await RemoveContentHandler.Handle(flag.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RemoveContentResponse>>();
        flag.Status.Should().Be(FlaggedContentStatus.ContentRemoved);
        integrationEvent.Should().NotBeNull();
        integrationEvent!.FlagId.Should().Be(flag.Id);
        integrationEvent.ContentType.Should().Be(nameof(ContentType.Media));
        integrationEvent.ContentId.Should().Be(contentId);
        session.Received(1).Store(Arg.Is<FlaggedContent[]>(arr => arr.Length == 1 && arr[0] == flag));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
