using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.FlagVerificationIssues;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - FlagVerificationIssuesHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class FlagVerificationIssuesHandlerTests
{
    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFound()
    {
        var shelterAccountId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<ShelterAccount>(shelterAccountId, null, out _);

        var result = await FlagVerificationIssuesHandler.Handle(
            shelterAccountId, new FlagVerificationIssuesRequest("Missing 501(c)(3) documentation"), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotInRequestedStatus_ReturnsConflict()
    {
        var shelterAccount = ShelterAccount.RequestNew(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        shelterAccount.Verify(); // already Verified, not Requested
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(shelterAccount.Id, shelterAccount, out _);

        var result = await FlagVerificationIssuesHandler.Handle(
            shelterAccount.Id, new FlagVerificationIssuesRequest("Missing 501(c)(3) documentation"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenRequested_FlagsIssuesAndPersists()
    {
        var shelterAccount = ShelterAccount.RequestNew(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(shelterAccount.Id, shelterAccount, out var stream);

        var result = await FlagVerificationIssuesHandler.Handle(
            shelterAccount.Id, new FlagVerificationIssuesRequest("Missing 501(c)(3) documentation"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<FlagVerificationIssuesResponse>>();
        shelterAccount.Status.Should().Be(ShelterAccountStatus.VerificationIssuesFound);
        shelterAccount.VerificationIssuesReason.Should().Be("Missing 501(c)(3) documentation");
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.ShelterAccountVerificationIssuesFoundV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
