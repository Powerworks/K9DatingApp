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
/// Layer 2 (TestingApproach.md) - ReviewApplicationHandler calls
/// FetchForWriting/AppendOne/SaveChangesAsync against Application plus a
/// plain LoadAsync against ShelterAccount for the ownership check
/// (read-only, not a self-load - ADR-031).
/// </summary>
public class ReviewApplicationHandlerTests
{
    private static readonly Guid ApplicantOwnerId = Guid.NewGuid();
    private static readonly Guid DogListingId = Guid.NewGuid();
    private static readonly Guid ShelterOwnerId = Guid.NewGuid();

    private static ClaimsPrincipal BuildUser(Guid ownerId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

    private static IDocumentSession BuildSession(ShelterAccount shelterAccount, Application? application, out JasperFx.Events.IEventStream<Application> stream)
    {
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting(application?.Id ?? Guid.NewGuid(), application, out stream);
        session.LoadAsync<ShelterAccount>(shelterAccount.Id, Arg.Any<CancellationToken>()).Returns(shelterAccount);
        return session;
    }

    [Fact]
    public async Task Handle_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var applicationId = Guid.NewGuid();
        var session = MartenEventStoreTestHelpers.BuildSessionWithFetchForWriting<Application>(applicationId, null, out _);

        var result = await ReviewApplicationHandler.Handle(applicationId, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Handle_WhenCallerDoesNotOwnTheShelter_ReturnsForbid()
    {
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, shelterAccount.Id, TestIntake.Default).Application;
        var session = BuildSession(shelterAccount, application, out _);

        var result = await ReviewApplicationHandler.Handle(application.Id, BuildUser(Guid.NewGuid()), session, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Handle_WhenApplicationIsNotPending_ReturnsConflict()
    {
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, shelterAccount.Id, TestIntake.Default).Application;
        application.Review(); // already UnderReview, not Pending
        var session = BuildSession(shelterAccount, application, out _);

        var result = await ReviewApplicationHandler.Handle(application.Id, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Conflict<string>>();
    }

    [Fact]
    public async Task Handle_WhenPendingAndCallerOwnsShelter_ReviewsAndPersists()
    {
        var shelterAccount = ShelterAccount.RequestNew(ShelterOwnerId, "Sunny Paws Rescue, EIN 12-3456789", Guid.NewGuid()).ShelterAccount;
        var application = Application.SubmitNew(ApplicantOwnerId, DogListingId, shelterAccount.Id, TestIntake.Default).Application;
        var session = BuildSession(shelterAccount, application, out var stream);

        var result = await ReviewApplicationHandler.Handle(application.Id, BuildUser(ShelterOwnerId), session, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ReviewApplicationResponse>>();
        application.Status.Should().Be(ApplicationStatus.UnderReview);
        stream.Received(1).AppendOne(Arg.Is<object>(o => o != null && o.GetType() == typeof(K9Crush.Modules.ShelterAdoption.Domain.Events.ApplicationReviewedV1)));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
