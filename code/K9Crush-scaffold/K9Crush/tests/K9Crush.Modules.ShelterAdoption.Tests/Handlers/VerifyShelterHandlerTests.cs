using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.VerifyShelter;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - VerifyShelterHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class VerifyShelterHandlerTests
{
    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFound()
    {
        var shelterAccountId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccountId, Arg.Any<CancellationToken>()).Returns((ShelterAccount?)null);

        var result = await VerifyShelterHandler.Handle(shelterAccountId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenNotInRequestedStatus_ReturnsConflict()
    {
        var shelterAccount = ShelterAccount.Create(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        shelterAccount.Verify(); // already Verified, not Requested
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await VerifyShelterHandler.Handle(shelterAccount.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenRequested_VerifiesAndPersists()
    {
        var shelterAccount = ShelterAccount.Create(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await VerifyShelterHandler.Handle(shelterAccount.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<VerifyShelterResponse>>();
        shelterAccount.Status.Should().Be(ShelterAccountStatus.Verified);
        session.Received(1).Store(Arg.Is<ShelterAccount[]>(arr => arr.Length == 1 && arr[0] == shelterAccount));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
