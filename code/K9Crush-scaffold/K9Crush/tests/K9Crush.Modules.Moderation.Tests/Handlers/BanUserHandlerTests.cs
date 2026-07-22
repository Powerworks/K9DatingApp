using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Moderation.Api.Commands.BanUser;
using K9Crush.Modules.Moderation.Domain;
using Xunit;

namespace K9Crush.Modules.Moderation.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - BanUserHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class BanUserHandlerTests
{
    [Fact]
    public async Task Handle_WhenFlagDoesNotExist_ReturnsNotFound()
    {
        var flagId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FlaggedContent>(flagId, Arg.Any<CancellationToken>()).Returns((FlaggedContent?)null);

        var result = await BanUserHandler.Handle(flagId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenOwnerWasWarnedButNotSuspended_ReturnsConflict()
    {
        var contentOwnerId = Guid.NewGuid();
        var flag = FlaggedContent.Create(ContentType.Media, Guid.NewGuid(), contentOwnerId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var record = UserModerationRecord.CreateFor(contentOwnerId);
        record.Warn(); // warned, but never suspended
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FlaggedContent>(flag.Id, Arg.Any<CancellationToken>()).Returns(flag);
        session.LoadAsync<UserModerationRecord>(contentOwnerId, Arg.Any<CancellationToken>()).Returns(record);

        var result = await BanUserHandler.Handle(flag.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenOwnerWasWarnedAndSuspended_BansAndPersists()
    {
        var contentOwnerId = Guid.NewGuid();
        var flag = FlaggedContent.Create(ContentType.Media, Guid.NewGuid(), contentOwnerId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var record = UserModerationRecord.CreateFor(contentOwnerId);
        record.Warn();
        record.Suspend();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FlaggedContent>(flag.Id, Arg.Any<CancellationToken>()).Returns(flag);
        session.LoadAsync<UserModerationRecord>(contentOwnerId, Arg.Any<CancellationToken>()).Returns(record);

        var result = await BanUserHandler.Handle(flag.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<BanUserResponse>>();
        record.IsBanned.Should().BeTrue();
        session.Received(1).Store(Arg.Is<UserModerationRecord[]>(arr => arr != null && arr.Length == 1 && arr[0] == record));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
