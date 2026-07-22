using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.ReadModels.GetApplicationStatus;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - GetApplicationStatusHandler only calls
/// IQuerySession.LoadAsync (no Query&lt;T&gt;() LINQ), so mocks cleanly here.
/// </summary>
public class GetApplicationStatusHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterAccountId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var applicationId = Guid.NewGuid();
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<Application>(applicationId, Arg.Any<CancellationToken>()).Returns((Application?)null);

        var result = await GetApplicationStatusHandler.Handle(applicationId, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheApplicant_ReturnsForbid()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default);
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await GetApplicationStatusHandler.Handle(application.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheApplicant_ReturnsStatusDetails()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId, TestIntake.Default);
        application.Review();
        application.RequestAdditionalDetails("Please provide vet references");
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await GetApplicationStatusHandler.Handle(application.Id, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ApplicationStatusResponse>>();
        var response = ((Ok<ApplicationStatusResponse>)result.Result).Value!;
        response.ApplicationId.Should().Be(application.Id);
        response.DogListingId.Should().Be(DogListingId);
        response.Status.Should().Be(nameof(ApplicationStatus.ReturnedForAlteration));
        response.AdditionalDetailsRequestReason.Should().Be("Please provide vet references");
        response.RejectionReason.Should().BeNull();
    }
}
