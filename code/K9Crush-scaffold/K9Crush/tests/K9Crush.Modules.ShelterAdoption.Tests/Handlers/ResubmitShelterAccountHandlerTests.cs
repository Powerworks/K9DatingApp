using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ResubmitShelterAccount;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ResubmitShelterAccountHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class ResubmitShelterAccountHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static ShelterAccount BuildFlaggedShelterAccount(Guid ownerId)
    {
        var shelterAccount = ShelterAccount.Create(ownerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        shelterAccount.FlagVerificationIssues("Missing 501(c)(3) documentation");
        return shelterAccount;
    }

    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFound()
    {
        var shelterAccountId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccountId, Arg.Any<CancellationToken>()).Returns((ShelterAccount?)null);

        var result = await ResubmitShelterAccountHandler.Handle(
            shelterAccountId, new ResubmitShelterAccountRequest("Updated details", Guid.NewGuid()), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheRequester_ReturnsForbid()
    {
        var shelterAccount = BuildFlaggedShelterAccount(OwnerId);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await ResubmitShelterAccountHandler.Handle(
            shelterAccount.Id, new ResubmitShelterAccountRequest("Updated details", Guid.NewGuid()), BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenNotInVerificationIssuesFoundStatus_ReturnsConflict()
    {
        var shelterAccount = ShelterAccount.Create(OwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()); // Requested, not VerificationIssuesFound
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await ResubmitShelterAccountHandler.Handle(
            shelterAccount.Id, new ResubmitShelterAccountRequest("Updated details", Guid.NewGuid()), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenFlaggedAndCallerIsRequester_ResubmitsAndPersists()
    {
        var shelterAccount = BuildFlaggedShelterAccount(OwnerId);
        var newUtilityBillId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await ResubmitShelterAccountHandler.Handle(
            shelterAccount.Id, new ResubmitShelterAccountRequest("Updated details", newUtilityBillId), BuildUser(OwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ResubmitShelterAccountResponse>>();
        shelterAccount.Status.Should().Be(ShelterAccountStatus.Requested);
        shelterAccount.BusinessDetails.Should().Be("Updated details");
        shelterAccount.UtilityBillDocumentId.Should().Be(newUtilityBillId);
        shelterAccount.VerificationIssuesReason.Should().BeNull();
        session.Received(1).Store(Arg.Is<ShelterAccount[]>(arr => arr != null && arr.Length == 1 && arr[0] == shelterAccount));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
