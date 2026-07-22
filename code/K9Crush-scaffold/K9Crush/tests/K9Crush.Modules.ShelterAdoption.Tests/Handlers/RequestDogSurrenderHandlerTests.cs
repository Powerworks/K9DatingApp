using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.RequestDogSurrender;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - RequestDogSurrenderHandler only calls
/// Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class RequestDogSurrenderHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static RequestDogSurrenderRequest BuildRequest() => new(
        "Cooper", "Terrier mix", 48, "Relocating for work", "Gentle, a little shy", "Up to date on vaccinations");

    [Fact]
    public async Task Handle_WhenCalled_CreatesRequestOwnedByCallerAndPersists()
    {
        var session = Substitute.For<IDocumentSession>();

        var result = await RequestDogSurrenderHandler.Handle(BuildRequest(), BuildUser(OwnerId), session, CancellationToken.None);

        result.Value!.SurrenderRequestId.Should().NotBeEmpty();
        session.Received(1).Store(Arg.Is<DogSurrenderRequest[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].RequestedByOwnerId == OwnerId && arr[0].DogName == "Cooper" &&
            arr[0].Status == SurrenderRequestStatus.Requested));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
