using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.CreateShelterAccount;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - CreateShelterAccountHandler only calls
/// FetchForWriting/AppendOne/SaveChangesAsync, so IDocumentSession mocks
/// cleanly here (ADR-031).
/// </summary>
public class CreateShelterAccountHandlerTests
{
    [Fact]
    public async Task Handle_WhenShelterAccountDoesNotExist_ReturnsNotFoundAndCascadesNothing()
    {
        var shelterAccountId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<ShelterAccount>(shelterAccountId, null, out _);

        var (result, integrationEvent) = await CreateShelterAccountHandler.Handle(shelterAccountId, session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenNotVerified_ReturnsConflictAndCascadesNothing()
    {
        var shelterAccount = ShelterAccount.RequestNew(Guid.NewGuid(), "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount; // Requested, not Verified
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(shelterAccount.Id, shelterAccount, out _);

        var (result, integrationEvent) = await CreateShelterAccountHandler.Handle(shelterAccount.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        integrationEvent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenVerified_ActivatesAndCascadesShelterAccountCreated()
    {
        var ownerId = Guid.NewGuid();
        var shelterAccount = ShelterAccount.RequestNew(ownerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        shelterAccount.Verify();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(shelterAccount.Id, shelterAccount, out var stream);

        var (result, integrationEvent) = await CreateShelterAccountHandler.Handle(shelterAccount.Id, session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<CreateShelterAccountResponse>>();
        shelterAccount.Status.Should().Be(ShelterAccountStatus.Created);
        integrationEvent.Should().NotBeNull();
        integrationEvent!.ShelterAccountId.Should().Be(shelterAccount.Id);
        integrationEvent.OwnerId.Should().Be(ownerId);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.ShelterAccountActivatedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
