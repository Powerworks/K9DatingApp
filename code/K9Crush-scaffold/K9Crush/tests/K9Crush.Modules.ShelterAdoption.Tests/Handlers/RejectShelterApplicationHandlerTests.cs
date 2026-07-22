using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.RejectShelterApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RejectShelterApplicationHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class RejectShelterApplicationHandlerTests
{
    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFound()
    {
        var shelterAccountId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccountId, Arg.Any<CancellationToken>()).Returns((ShelterAccount?)null);

        var result = await RejectShelterApplicationHandler.Handle(
            shelterAccountId, new RejectShelterApplicationRequest("Cannot verify legitimacy"), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotInVerificationIssuesFoundStatus_ReturnsConflict()
    {
        var shelterAccount = ShelterAccount.Create(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()); // Requested, not VerificationIssuesFound
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await RejectShelterApplicationHandler.Handle(
            shelterAccount.Id, new RejectShelterApplicationRequest("Cannot verify legitimacy"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenFlagged_RejectsAndPersists()
    {
        var shelterAccount = ShelterAccount.Create(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        shelterAccount.FlagVerificationIssues("Missing 501(c)(3) documentation");
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await RejectShelterApplicationHandler.Handle(
            shelterAccount.Id, new RejectShelterApplicationRequest("Cannot verify legitimacy"), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RejectShelterApplicationResponse>>();
        shelterAccount.Status.Should().Be(ShelterAccountStatus.Rejected);
        shelterAccount.RejectionReason.Should().Be("Cannot verify legitimacy");
        session.Received(1).Store(Arg.Is<ShelterAccount[]>(arr => arr != null && arr.Length == 1 && arr[0] == shelterAccount));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
