using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.WithdrawApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - WithdrawApplicationHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class WithdrawApplicationHandlerTests
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
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(applicationId, Arg.Any<CancellationToken>()).Returns((Application?)null);

        var result = await WithdrawApplicationHandler.Handle(applicationId, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheApplicant_ReturnsForbid()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await WithdrawApplicationHandler.Handle(application.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenAlreadyApproved_ReturnsConflictAndDoesNotWithdraw()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);
        application.Review();
        application.Approve();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await WithdrawApplicationHandler.Handle(application.Id, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
        application.Status.Should().Be(ApplicationStatus.Approved);
    }

    [Fact]
    public async Task Handle_WhenOwnedByCallerAndNotApproved_WithdrawsAndPersists()
    {
        var application = Application.Submit(ApplicantOwnerId, DogListingId, ShelterAccountId);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var result = await WithdrawApplicationHandler.Handle(application.Id, BuildUser(ApplicantOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<WithdrawApplicationResponse>>();
        application.Status.Should().Be(ApplicationStatus.Withdrawn);
        session.Received(1).Store(Arg.Is<Application[]>(arr => arr.Length == 1 && arr[0] == application));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
