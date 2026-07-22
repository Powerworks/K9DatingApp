using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ApproveShelterAccount;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ApproveShelterAccountHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class ApproveShelterAccountHandlerTests
{
    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFoundAndCascadesNothing()
    {
        var shelterAccountId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccountId, Arg.Any<CancellationToken>()).Returns((ShelterAccount?)null);

        var (result, integrationEvent) = await ApproveShelterAccountHandler.Handle(shelterAccountId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenNotInVerificationIssuesFoundStatus_ReturnsConflictAndCascadesNothing()
    {
        var shelterAccount = ShelterAccount.Create(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()); // Requested, not VerificationIssuesFound
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var (result, integrationEvent) = await ApproveShelterAccountHandler.Handle(shelterAccount.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenFlagged_ActivatesDespiteIssuesAndCascadesShelterAccountCreated()
    {
        var ownerId = Guid.NewGuid();
        var shelterAccount = ShelterAccount.Create(ownerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        shelterAccount.FlagVerificationIssues("Missing 501(c)(3) documentation");
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var (result, integrationEvent) = await ApproveShelterAccountHandler.Handle(shelterAccount.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ApproveShelterAccountResponse>>();
        shelterAccount.Status.Should().Be(ShelterAccountStatus.Created);
        integrationEvent.Should().NotBeNull();
        integrationEvent!.ShelterAccountId.Should().Be(shelterAccount.Id);
        integrationEvent.OwnerId.Should().Be(ownerId);
        session.Received(1).Store(Arg.Is<ShelterAccount[]>(arr => arr != null && arr.Length == 1 && arr[0] == shelterAccount));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
