using System.Security.Claims;
using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.RequestShelterAccount;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RequestShelterAccountHandler only calls
/// Events.StartStream/SaveChangesAsync (no LoadAsync - ShelterAccount is
/// always newly created), so IDocumentSession mocks cleanly here
/// (ADR-031).
/// </summary>
public class RequestShelterAccountHandlerTests
{
    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenCalled_CreatesRequestedShelterAccountAndPersists()
    {
        var ownerId = Guid.NewGuid();
        var utilityBillDocumentId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();

        var response = await RequestShelterAccountHandler.Handle(
            new RequestShelterAccountRequest("Sunny Paws Rescue, EIN 12-3456789", utilityBillDocumentId),
            BuildUser(ownerId), session, CancellationToken.None);

        response.ShelterAccountId.Should().NotBeEmpty();

        session.Events.Received(1).StartStream<ShelterAccount>(
            response.ShelterAccountId,
            Arg.Is<object[]>(events => events != null && events.Length == 1 && events[0] != null
                && ((K9Crush.Modules.ShelterAdoption.Domain.Events.ShelterAccountRequestedV1)events[0]).RequestedByOwnerId == ownerId
                && ((K9Crush.Modules.ShelterAdoption.Domain.Events.ShelterAccountRequestedV1)events[0]).BusinessDetails == "Sunny Paws Rescue, EIN 12-3456789"
                && ((K9Crush.Modules.ShelterAdoption.Domain.Events.ShelterAccountRequestedV1)events[0]).UtilityBillDocumentId == utilityBillDocumentId));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
