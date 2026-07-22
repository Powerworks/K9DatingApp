using System.Security.Claims;
using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ApplyToFoster;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ApplyToFosterHandler only calls
/// Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class ApplyToFosterHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenCalled_CreatesApplicationOwnedByCallerAndPersists()
    {
        var session = Substitute.For<IDocumentSession>();
        var availableFrom = new DateOnly(2026, 8, 1);
        var request = new ApplyToFosterRequest(HomeType.House, HasGarden: true, HasOtherPets: false, availableFrom);

        var result = await ApplyToFosterHandler.Handle(request, BuildUser(OwnerId), session, CancellationToken.None);

        result.Value!.FosterApplicationId.Should().NotBeEmpty();
        session.Received(1).Store(Arg.Is<FosterApplication[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].ApplicantOwnerId == OwnerId &&
            arr[0].HomeType == HomeType.House && arr[0].AvailableFrom == availableFrom &&
            arr[0].Status == FosterApplicationStatus.Submitted));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
