using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Moderation.Api.Commands.SuspendUser;
using K9Crush.Modules.Moderation.Domain;
using Xunit;

namespace K9Crush.Modules.Moderation.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - SuspendUserHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class SuspendUserHandlerTests
{
    [Fact]
    public async Task Handle_WhenFlagDoesNotExist_ReturnsNotFound()
    {
        var flagId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FlaggedContent>(flagId, Arg.Any<CancellationToken>()).Returns((FlaggedContent?)null);

        var result = await SuspendUserHandler.Handle(flagId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenOwnerHasNeverBeenWarned_ReturnsConflict()
    {
        var contentOwnerId = Guid.NewGuid();
        var flag = FlaggedContent.Create(ContentType.Media, Guid.NewGuid(), contentOwnerId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FlaggedContent>(flag.Id, Arg.Any<CancellationToken>()).Returns(flag);
        session.LoadAsync<UserModerationRecord>(contentOwnerId, Arg.Any<CancellationToken>()).Returns((UserModerationRecord?)null);

        var result = await SuspendUserHandler.Handle(flag.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenOwnerWasAlreadyWarned_SuspendsAndPersists()
    {
        var contentOwnerId = Guid.NewGuid();
        var flag = FlaggedContent.Create(ContentType.Media, Guid.NewGuid(), contentOwnerId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var record = UserModerationRecord.CreateFor(contentOwnerId);
        record.Warn();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FlaggedContent>(flag.Id, Arg.Any<CancellationToken>()).Returns(flag);
        session.LoadAsync<UserModerationRecord>(contentOwnerId, Arg.Any<CancellationToken>()).Returns(record);

        var result = await SuspendUserHandler.Handle(flag.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<SuspendUserResponse>>();
        record.IsSuspended.Should().BeTrue();
        session.Received(1).Store(Arg.Is<UserModerationRecord[]>(arr => arr.Length == 1 && arr[0] == record));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
