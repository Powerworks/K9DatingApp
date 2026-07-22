using System.Security.Claims;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using K9Crush.Modules.ShelterAdoption.Api.Commands.ReviewApplication;
using K9Crush.Modules.ShelterAdoption.Domain;
using Xunit;

namespace K9Crush.Modules.ShelterAdoption.Tests.Handlers;

/// <summary>
/// Layer 2 (TestingApproach.md) - ReviewApplicationHandler only calls
/// LoadAsync/Store/SaveChangesAsync, so IDocumentSession mocks cleanly here.
/// </summary>
public class ReviewApplicationHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var applicationId = Guid.NewGuid();
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(applicationId, Arg.Any<CancellationToken>()).Returns((Application?)null);

        var result = await ReviewApplicationHandler.Handle(applicationId, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnTheShelter_ReturnsForbid()
    {
        var shelterAccount = ShelterAccount.Create(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var application = Application.Submit(ApplicantOwnerId, DogListingId, shelterAccount.Id);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await ReviewApplicationHandler.Handle(application.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenApplicationIsNotPending_ReturnsConflict()
    {
        var shelterAccount = ShelterAccount.Create(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var application = Application.Submit(ApplicantOwnerId, DogListingId, shelterAccount.Id);
        application.Review(); // already UnderReview, not Pending
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await ReviewApplicationHandler.Handle(application.Id, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenPendingAndCallerOwnsShelter_ReviewsAndPersists()
    {
        var shelterAccount = ShelterAccount.Create(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid());
        var application = Application.Submit(ApplicantOwnerId, DogListingId, shelterAccount.Id);
        var session = Substitute.For<IDocumentSession>();
        session.LoadAsync<Application>(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);

        var result = await ReviewApplicationHandler.Handle(application.Id, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ReviewApplicationResponse>>();
        application.Status.Should().Be(ApplicationStatus.UnderReview);
        session.Received(1).Store(Arg.Is<Application[]>(arr => arr != null && arr.Length == 1 && arr[0] == application));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
