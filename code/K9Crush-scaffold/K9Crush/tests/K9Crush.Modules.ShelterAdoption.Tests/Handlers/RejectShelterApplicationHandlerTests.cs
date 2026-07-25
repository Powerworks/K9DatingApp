using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.RejectShelterApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RejectShelterApplicationHandler only
/// calls FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession
/// mocks cleanly here (ADR-031).
/// </summary>
public class RejectShelterApplicationHandlerTests
{
    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFound()
    {
        var shelterAccountId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<ShelterAccount>(shelterAccountId, null, out _);

        var result = await RejectShelterApplicationHandler.Handle(
            shelterAccountId, new RejectShelterApplicationRequest("Cannot verify legitimacy"), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotInVerificationIssuesFoundStatus_ReturnsConflict()
    {
        var shelterAccount = ShelterAccount.RequestNew(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount; // Requested, not VerificationIssuesFound
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(shelterAccount.Id, shelterAccount, out _);

        var result = await RejectShelterApplicationHandler.Handle(
            shelterAccount.Id, new RejectShelterApplicationRequest("Cannot verify legitimacy"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenFlagged_RejectsAndPersists()
    {
        var shelterAccount = ShelterAccount.RequestNew(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        shelterAccount.FlagVerificationIssues("Missing 501(c)(3) documentation");
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(shelterAccount.Id, shelterAccount, out var stream);

        var result = await RejectShelterApplicationHandler.Handle(
            shelterAccount.Id, new RejectShelterApplicationRequest("Cannot verify legitimacy"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RejectShelterApplicationResponse>>();
        shelterAccount.Status.Should().Be(ShelterAccountStatus.Rejected);
        shelterAccount.RejectionReason.Should().Be("Cannot verify legitimacy");
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.ShelterAccountRejectedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
