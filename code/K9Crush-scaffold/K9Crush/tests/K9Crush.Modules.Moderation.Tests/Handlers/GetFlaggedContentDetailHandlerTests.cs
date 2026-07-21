using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.Moderation.Api.ReadModels.GetFlaggedContentDetail;
using K9Crush.Modules.Moderation.Domain;
using Xunit;

namespace K9Crush.Modules.Moderation.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - GetFlaggedContentDetailHandler only calls
/// IQuerySession.LoadAsync (no Query&lt;T&gt;() LINQ), so mocks cleanly here.
/// </summary>
public class GetFlaggedContentDetailHandlerTests
{
    [Fact]
    public async Task Handle_WhenFlagDoesNotExist_ReturnsNotFound()
    {
        var flagId = Guid.NewGuid();
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<FlaggedContent>(flagId, Arg.Any<CancellationToken>()).Returns((FlaggedContent?)null);

        var result = await GetFlaggedContentDetailHandler.Handle(flagId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenFlagExists_ReturnsDetail()
    {
        var flag = FlaggedContent.Create(ContentType.Media, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<FlaggedContent>(flag.Id, Arg.Any<CancellationToken>()).Returns(flag);

        var result = await GetFlaggedContentDetailHandler.Handle(flag.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<FlaggedContentDetailResponse>>();
        var response = ((Ok<FlaggedContentDetailResponse>)result.Result).Value!;
        response.FlagId.Should().Be(flag.Id);
        response.ContentType.Should().Be(nameof(ContentType.Media));
        response.Status.Should().Be(nameof(FlaggedContentStatus.Open));
    }
}
