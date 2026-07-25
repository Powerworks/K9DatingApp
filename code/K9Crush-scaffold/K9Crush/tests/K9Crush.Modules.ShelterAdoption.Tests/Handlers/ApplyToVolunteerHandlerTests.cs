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
/// Events.StartStream/SaveChangesAsync, so IDocumentSession mocks cleanly
/// here (ADR-031).
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

        var volunteerApplicationId = result.Value!.VolunteerApplicationId;
        volunteerApplicationId.Should().NotBeEmpty();

        session.Events.Received(1).StartStream<VolunteerApplication>(
            volunteerApplicationId,
            Arg.Is<object[]>(events => events != null && events.Length == 1 && events[0] != null
                && ((K9Crush.Modules.ShelterAdoption.Domain.Events.VolunteerApplicationSubmittedV1)events[0]).ApplicantOwnerId == OwnerId
                && ((K9Crush.Modules.ShelterAdoption.Domain.Events.VolunteerApplicationSubmittedV1)events[0]).AreaOfInterest == VolunteerAreaOfInterest.HomeChecks));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
