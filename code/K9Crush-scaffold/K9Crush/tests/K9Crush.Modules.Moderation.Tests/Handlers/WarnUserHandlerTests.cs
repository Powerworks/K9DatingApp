using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Moderation.Api.Commands.WarnUser;
using K9Crush.Modules.Moderation.Domain;
using Xunit;

namespace K9Crush.Modules.Moderation.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - WarnUserHandler only calls LoadAsync
/// (twice, two different document types)/Store/SaveChangesAsync, so
/// IDocumentSession mocks cleanly here.
/// </summary>
public class WarnUserHandlerTests
{
    [Fact]
    public async Task Handle_WhenFlagDoesNotExist_ReturnsNotFound()
    {
        var flagId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FlaggedContent>(flagId, Arg.Any<CancellationToken>()).Returns((FlaggedContent?)null);

        var result = await WarnUserHandler.Handle(flagId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNoRecordExistsYet_CreatesOneAndWarnsForTheFirstTime()
    {
        var contentOwnerId = Guid.NewGuid();
        var flag = FlaggedContent.Create(ContentType.Media, Guid.NewGuid(), contentOwnerId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FlaggedContent>(flag.Id, Arg.Any<CancellationToken>()).Returns(flag);
        session.LoadAsync<UserModerationRecord>(contentOwnerId, Arg.Any<CancellationToken>()).Returns((UserModerationRecord?)null);

        var result = await WarnUserHandler.Handle(flag.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<WarnUserResponse>>();
        var response = ((Ok<WarnUserResponse>)result.Result).Value!;
        response.OwnerId.Should().Be(contentOwnerId);
        response.WarningCount.Should().Be(1);
        session.Received(1).Store(Arg.Is<UserModerationRecord[]>(arr => arr != null && arr.Length == 1 && arr[0].Id == contentOwnerId && arr[0].WarningCount == 1));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRecordAlreadyExists_IncrementsWarningCount()
    {
        var contentOwnerId = Guid.NewGuid();
        var flag = FlaggedContent.Create(ContentType.Media, Guid.NewGuid(), contentOwnerId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var existingRecord = UserModerationRecord.CreateFor(contentOwnerId);
        existingRecord.Warn();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<FlaggedContent>(flag.Id, Arg.Any<CancellationToken>()).Returns(flag);
        session.LoadAsync<UserModerationRecord>(contentOwnerId, Arg.Any<CancellationToken>()).Returns(existingRecord);

        var result = await WarnUserHandler.Handle(flag.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<WarnUserResponse>>();
        ((Ok<WarnUserResponse>)result.Result).Value!.WarningCount.Should().Be(2);
    }
}
