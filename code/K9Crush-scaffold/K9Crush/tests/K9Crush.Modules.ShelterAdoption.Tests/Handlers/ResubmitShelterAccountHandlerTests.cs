using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ResubmitShelterAccount;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ResubmitShelterAccountHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class ResubmitShelterAccountHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static ShelterAccount BuildFlaggedShelterAccount(Guid ownerId)
    {
        var shelterAccount = ShelterAccount.RequestNew(ownerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        shelterAccount.FlagVerificationIssues("Missing 501(c)(3) documentation");
        return shelterAccount;
    }

    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFound()
    {
        var shelterAccountId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<ShelterAccount>(shelterAccountId, null, out _);

        var result = await ResubmitShelterAccountHandler.Handle(
            shelterAccountId, new ResubmitShelterAccountRequest("Updated details", Guid.NewGuid()), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheRequester_ReturnsForbid()
    {
        var shelterAccount = BuildFlaggedShelterAccount(OwnerId);
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(shelterAccount.Id, shelterAccount, out _);

        var result = await ResubmitShelterAccountHandler.Handle(
            shelterAccount.Id, new ResubmitShelterAccountRequest("Updated details", Guid.NewGuid()), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenNotInVerificationIssuesFoundStatus_ReturnsConflict()
    {
        var shelterAccount = ShelterAccount.RequestNew(OwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount; // Requested, not VerificationIssuesFound
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(shelterAccount.Id, shelterAccount, out _);

        var result = await ResubmitShelterAccountHandler.Handle(
            shelterAccount.Id, new ResubmitShelterAccountRequest("Updated details", Guid.NewGuid()), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenFlaggedAndCallerIsRequester_ResubmitsAndPersists()
    {
        var shelterAccount = BuildFlaggedShelterAccount(OwnerId);
        var newUtilityBillId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(shelterAccount.Id, shelterAccount, out var stream);

        var result = await ResubmitShelterAccountHandler.Handle(
            shelterAccount.Id, new ResubmitShelterAccountRequest("Updated details", newUtilityBillId), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ResubmitShelterAccountResponse>>();
        shelterAccount.Status.Should().Be(ShelterAccountStatus.Requested);
        shelterAccount.BusinessDetails.Should().Be("Updated details");
        shelterAccount.UtilityBillDocumentId.Should().Be(newUtilityBillId);
        shelterAccount.VerificationIssuesReason.Should().BeNull();
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.ShelterAccountResubmittedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
