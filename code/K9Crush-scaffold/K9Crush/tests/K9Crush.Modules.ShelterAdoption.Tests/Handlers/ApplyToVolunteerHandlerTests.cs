using System.Security.Claims;
using FluentAssertions;
using Marten;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ApplyToVolunteer;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ApplyToVolunteerHandler only calls
/// Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class ApplyToVolunteerHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenCalled_CreatesApplicationOwnedByCallerAndPersists()
    {
        var session = Substitute.For<IDocumentSession>();
        var request = new ApplyToVolunteerRequest(VolunteerAreaOfInterest.HomeChecks);

        var result = await ApplyToVolunteerHandler.Handle(request, BuildUser(OwnerId), session, CancellationToken.None);

        result.Value!.VolunteerApplicationId.Should().NotBeEmpty();
        session.Received(1).Store(Arg.Is<VolunteerApplication[]>(arr =>
            arr != null && arr.Length == 1 && arr[0].ApplicantOwnerId == OwnerId &&
            arr[0].AreaOfInterest == VolunteerAreaOfInterest.HomeChecks &&
            arr[0].Status == VolunteerApplicationStatus.Submitted));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
